using System;
using System.Collections.Generic;
using System.Globalization;
using Dreamy.Datasave;
using Dreamy.Economy;
using Dreamy.Economy.Rewards;

namespace Dreamy.DailyReward
{
    public sealed class DailyRewardModel : IDailyRewardService
    {
        private readonly DailyRewardScheduleConfig schedule;
        private readonly IDatasaveService datasave;
        private readonly IResourceWallet resourceWallet;
        private readonly IRewardDeliveryGateway gateway;
        private readonly IDailyRewardBundleResolver resolver;
        private readonly IRewardClock clock;
        private readonly string saveKey;
        private DailyRewardSaveData save;
        private bool busy;

        public DailyRewardModel(
            DailyRewardScheduleConfig schedule,
            IDatasaveService datasave,
            IResourceWallet resourceWallet,
            IRewardClock clock,
            string saveKey)
        {
            this.schedule = schedule ?? throw new ArgumentNullException(nameof(schedule));
            this.datasave = datasave ?? throw new ArgumentNullException(nameof(datasave));
            this.resourceWallet = resourceWallet ?? throw new ArgumentNullException(nameof(resourceWallet));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.saveKey = string.IsNullOrWhiteSpace(saveKey)
                ? throw new ArgumentException("Save key cannot be empty.", nameof(saveKey))
                : saveKey;
            foreach (DailyRewardEntry entry in schedule.Rewards)
                if (entry.IsBundle) throw new ArgumentException("Bundle schedules require a reward delivery gateway.", nameof(schedule));
            InitializeSave();
        }

        public DailyRewardModel(
            DailyRewardScheduleConfig schedule,
            IDatasaveService datasave,
            IRewardDeliveryGateway gateway,
            IRewardClock clock,
            string saveKey)
            : this(schedule, datasave, gateway, clock, saveKey, null)
        {
        }

        public DailyRewardModel(
            DailyRewardScheduleConfig schedule,
            IDatasaveService datasave,
            IRewardDeliveryGateway gateway,
            IRewardClock clock,
            string saveKey,
            IDailyRewardBundleResolver resolver)
        {
            this.schedule = schedule ?? throw new ArgumentNullException(nameof(schedule));
            this.datasave = datasave ?? throw new ArgumentNullException(nameof(datasave));
            this.gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
            this.resolver = resolver;
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.saveKey = string.IsNullOrWhiteSpace(saveKey)
                ? throw new ArgumentException("Save key cannot be empty.", nameof(saveKey))
                : saveKey;
            InitializeSave();
        }

        public DailyRewardViewState GetState()
        {
            DailyRewardScheduleConfig active = GetActiveSchedule();
            Progress progress = GetProgress(clock.UtcNow.Date, active);
            return CreateState(progress, active);
        }

        public DailyRewardClaimResult Claim()
        {
            if (busy) return DailyRewardClaimResult.Busy();
            busy = true;
            try
            {
                // Pending recovery always precedes date, revision, and eligibility evaluation.
                if (HasPendingClaim)
                    return gateway == null
                        ? DailyRewardClaimResult.MigrationRequired()
                        : DeliverPending(new RewardDeliveryCoordinator(gateway, save.Journal));

                if (RequiresMigration) return DailyRewardClaimResult.MigrationRequired();
                DateTime today = clock.UtcNow.Date;
                DailyRewardScheduleConfig active = GetActiveSchedule();
                Progress progress = GetProgress(today, active);
                if (progress.IsCompleted) return DailyRewardClaimResult.Completed();
                if (!progress.CanClaim) return DailyRewardClaimResult.NotAvailable();

                DailyRewardEntry entry = active.Rewards[progress.NextRewardIndex];
                int nextIndex = progress.NextRewardIndex + 1;
                int nextCycle = progress.Cycle;
                if (nextIndex == active.Rewards.Count && active.CycleMode == DailyRewardCycleMode.Repeat)
                {
                    nextIndex = 0;
                    nextCycle = checked(nextCycle + 1);
                }

                if (gateway == null)
                {
                    if (entry.IsBundle) return DailyRewardClaimResult.MigrationRequired();
                    ResourceAmount reward = entry.Reward;
                    // Preserve the released single-resource transaction ID, without pretending
                    // an arbitrary bool wallet has the strict gateway's persistence guarantees.
                    if (!resourceWallet.TryGrant(new ResourceGrantRequest(
                            $"{active.ScheduleId}:{progress.Cycle}:{entry.Id}", reward)))
                        return DailyRewardClaimResult.GrantFailed();
                    DailyRewardSaveData candidate = save.Clone();
                    candidate.ActiveSchedule = active;
                    candidate.NextRewardIndex = nextIndex;
                    candidate.Cycle = nextCycle;
                    candidate.LastClaimUtcTicks = today.Ticks;
                    try { Commit(candidate); }
                    catch (Exception) { return DailyRewardClaimResult.Delivery(RewardDeliveryResult.Pending(RewardDeliveryReason.PersistenceFailed)); }
                    return DailyRewardClaimResult.Claimed(reward);
                }

                ResolvedRewardBundle bundle = entry.GetBundle();
                var pending = new DailyRewardPendingClaim
                {
                    ClaimUtcTicks = today.Ticks, ClaimCycle = progress.Cycle, EntryId = entry.Id,
                    Revision = active.Revision, NextRewardIndex = nextIndex, NextCycle = nextCycle,
                    BundleSchemaVersion = bundle.SchemaVersion, ResolverVersion = bundle.ResolverVersion,
                    RequiresResolution = entry.RequiresResolution
                };
                foreach (ResolvedRewardEntry reward in bundle.Entries)
                    pending.Entries.Add(new RewardJournalEntry
                    {
                        EntryId = reward.EntryId, KindId = reward.KindId, TargetId = reward.TargetId, Amount = reward.Amount
                    });
                string requestId = RewardIdentity.Request(saveKey, "daily", active.ScheduleId,
                    progress.Cycle.ToString(CultureInfo.InvariantCulture), entry.Id);
                string fingerprint = RewardIdentity.Fingerprint("daily-claim", requestId, bundle.Fingerprint);
                var coordinator = new RewardDeliveryCoordinator(gateway);
                RewardDeliveryResult intent = coordinator.Begin(requestId, fingerprint, journal =>
                {
                    DailyRewardSaveData candidate = save.Clone();
                    candidate.ActiveSchedule = active;
                    candidate.PendingClaim = pending.Clone();
                    candidate.Journal = journal.Clone();
                    Commit(candidate);
                });
                return intent.IsSuccess ? DeliverPending(coordinator) : DailyRewardClaimResult.Delivery(intent);
            }
            finally { busy = false; }
        }

        private DailyRewardClaimResult DeliverPending(RewardDeliveryCoordinator coordinator)
        {
            if (save.PendingClaim == null || save.Journal == null || !save.Journal.IsValid())
                return DailyRewardClaimResult.Delivery(RewardDeliveryResult.Failed(RewardDeliveryReason.InvalidJournal));
            ResolvedRewardBundle bundle;
            if (save.Journal.State == RewardJournalState.IntentPersisted)
            {
                try { bundle = save.PendingClaim.GetBundle(); }
                catch (ArgumentException)
                {
                    return DailyRewardClaimResult.Delivery(RewardDeliveryResult.Failed(RewardDeliveryReason.InvalidJournal));
                }
                // The persisted intent binds the original payload before any authority call.
                // A modified duplicate snapshot must not turn the same request into a new grant.
                string fingerprint = RewardIdentity.Fingerprint("daily-claim", save.Journal.RequestId, bundle.Fingerprint);
                if (!string.Equals(fingerprint, save.Journal.RequestFingerprint, StringComparison.Ordinal))
                    return DailyRewardClaimResult.Delivery(RewardDeliveryResult.Failed(RewardDeliveryReason.PayloadMismatch));
                if (save.PendingClaim.RequiresResolution && resolver == null)
                    return DailyRewardClaimResult.Delivery(RewardDeliveryResult.Pending(RewardDeliveryReason.ResolutionRequired));
                if (resolver != null)
                {
                    RewardDeliveryResult resolution;
                    try { resolution = resolver.Resolve(save.Journal.RequestId, bundle, out bundle); }
                    catch (Exception)
                    {
                        return DailyRewardClaimResult.Delivery(RewardDeliveryResult.Pending(RewardDeliveryReason.DestinationUnavailable));
                    }
                    if (!resolution.IsSuccess) return DailyRewardClaimResult.Delivery(resolution);
                    if (bundle == null)
                        return DailyRewardClaimResult.Delivery(RewardDeliveryResult.Failed(RewardDeliveryReason.InvalidRequest));
                }
                RewardDeliveryResult resolved = coordinator.Resolve(bundle, journal =>
                {
                    DailyRewardSaveData candidate = save.Clone();
                    candidate.Journal = journal.Clone();
                    candidate.PendingClaim.BundleSchemaVersion = bundle.SchemaVersion;
                    candidate.PendingClaim.ResolverVersion = bundle.ResolverVersion;
                    candidate.PendingClaim.Entries.Clear();
                    foreach (ResolvedRewardEntry reward in bundle.Entries)
                        candidate.PendingClaim.Entries.Add(new RewardJournalEntry
                        {
                            EntryId = reward.EntryId, KindId = reward.KindId,
                            TargetId = reward.TargetId, Amount = reward.Amount
                        });
                    Commit(candidate);
                });
                if (!resolved.IsSuccess) return DailyRewardClaimResult.Delivery(resolved);
            }
            // The journal, not the current resolver/config, is authoritative on recovery.
            bundle = save.Journal.GetResolvedBundle();
            RewardDeliveryResult delivered = coordinator.Deliver(PersistJournal);
            if (!delivered.IsSuccess) return DailyRewardClaimResult.Delivery(delivered);
            RewardDeliveryResult completed = coordinator.Complete(journal =>
            {
                DailyRewardSaveData candidate = save.Clone();
                candidate.NextRewardIndex = candidate.PendingClaim.NextRewardIndex;
                candidate.Cycle = candidate.PendingClaim.NextCycle;
                candidate.LastClaimUtcTicks = candidate.PendingClaim.ClaimUtcTicks;
                candidate.Journal = journal.Clone();
                Commit(candidate);
            });
            if (!completed.IsSuccess) return DailyRewardClaimResult.Delivery(completed);
            DailyRewardEntry entry = null;
            foreach (DailyRewardEntry reward in save.ActiveSchedule.Rewards)
                if (reward.Id == save.PendingClaim.EntryId) { entry = reward; break; }
            return DailyRewardClaimResult.Claimed(bundle, entry != null && !entry.IsBundle ? entry.Reward : (ResourceAmount?)null);
        }

        private void PersistJournal(RewardJournal journal)
        {
            DailyRewardSaveData candidate = save.Clone();
            candidate.Journal = journal.Clone();
            Commit(candidate);
        }

        private void Commit(DailyRewardSaveData candidate)
        {
            datasave.Save(candidate, saveKey);
            save = candidate;
        }

        private bool HasPendingClaim => save.Journal != null && save.Journal.State != RewardJournalState.Completed;
        private bool RequiresMigration => save.ScheduleId != schedule.ScheduleId ||
            save.NextRewardIndex < 0 || save.NextRewardIndex > save.ActiveSchedule.Rewards.Count;

        private DailyRewardScheduleConfig GetActiveSchedule()
        {
            if (HasPendingClaim || RequiresMigration) return save.ActiveSchedule;
            // Freeze a changed revision for this cycle. Adopt it only before the first claim
            // of the next cycle, never by erasing progress or replacing a pending payload.
            return save.NextRewardIndex == 0 || save.ActiveSchedule.Fingerprint == schedule.Fingerprint
                ? schedule : save.ActiveSchedule;
        }

        private DailyRewardViewState CreateState(Progress progress, DailyRewardScheduleConfig active)
        {
            List<DailyRewardItemViewState> items = new(active.Rewards.Count);
            for (int index = 0; index < active.Rewards.Count; index++)
            {
                DailyRewardItemStatus status = ResolveItemStatus(index, progress);
                items.Add(new DailyRewardItemViewState(active.Rewards[index], status));
            }

            return new DailyRewardViewState(items,
                !busy && (HasPendingClaim ? gateway != null : !RequiresMigration && progress.CanClaim),
                progress.NextClaimUtc, HasPendingClaim, busy, RequiresMigration, progress.IsCompleted);
        }

        private DailyRewardItemStatus ResolveItemStatus(int index, Progress progress)
        {
            if (progress.IsCompleted) return DailyRewardItemStatus.Claimed;
            if (index > progress.NextRewardIndex)
            {
                return DailyRewardItemStatus.Locked;
            }

            if (index < progress.NextRewardIndex)
            {
                return DailyRewardItemStatus.Claimed;
            }

            return progress.CanClaim
                ? DailyRewardItemStatus.Claimable
                : DailyRewardItemStatus.Cooldown;
        }

        private Progress GetProgress(DateTime today, DailyRewardScheduleConfig active)
        {
            int nextIndex = save.NextRewardIndex;
            int cycle = save.Cycle;
            if (nextIndex < 0 || nextIndex > active.Rewards.Count)
            {
                return new Progress(0, cycle, false, false, DateTime.MinValue);
            }

            if (nextIndex == active.Rewards.Count)
            {
                return new Progress(nextIndex, cycle, false, true, DateTime.MinValue);
            }

            DateTime lastClaimDate = GetLastClaimDate();
            if (lastClaimDate == DateTime.MinValue)
            {
                return new Progress(nextIndex, cycle, true, false, DateTime.MinValue);
            }

            int elapsedDays = (today - lastClaimDate).Days;
            if (elapsedDays <= 0)
            {
                return new Progress(nextIndex, cycle, false, false, lastClaimDate.AddDays(1));
            }

            if (elapsedDays > 1 && active.ResetProgressAfterMissedDay)
            {
                nextIndex = 0;
                cycle = checked(cycle + 1);
            }

            return new Progress(nextIndex, cycle, true, false, DateTime.MinValue);
        }

        private void InitializeSave()
        {
            save = datasave.Load<DailyRewardSaveData>(saveKey).Clone();
            if (save.ActiveSchedule != null) return;
            DailyRewardSaveData candidate = save.Clone();
            // v1 has no revision snapshot; preserve its counters and claim date.
            // A different identity needs an explicit host migration, not a reset.
            if (string.IsNullOrEmpty(candidate.ScheduleId)) candidate.ScheduleId = schedule.ScheduleId;
            candidate.ActiveSchedule = schedule;
            Commit(candidate);
        }

        private DateTime GetLastClaimDate()
        {
            return save.LastClaimUtcTicks <= 0
                ? DateTime.MinValue
                : new DateTime(save.LastClaimUtcTicks, DateTimeKind.Utc).Date;
        }

        private readonly struct Progress
        {
            public Progress(int nextRewardIndex, int cycle, bool canClaim, bool isCompleted, DateTime nextClaimUtc)
            {
                NextRewardIndex = nextRewardIndex;
                Cycle = cycle;
                CanClaim = canClaim;
                IsCompleted = isCompleted;
                NextClaimUtc = nextClaimUtc;
            }

            public int NextRewardIndex { get; }
            public int Cycle { get; }
            public bool CanClaim { get; }
            public bool IsCompleted { get; }
            public DateTime NextClaimUtc { get; }
        }
    }
}
