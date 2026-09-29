using System;
using System.Collections.Generic;
using Dreamy.Datasave;
using Dreamy.Economy;

namespace Dreamy.DailyReward
{
    public sealed class DailyRewardModel : IDailyRewardService
    {
        private readonly DailyRewardScheduleConfig schedule;
        private readonly IDatasaveService datasave;
        private readonly IResourceWallet resourceWallet;
        private readonly IRewardClock clock;
        private readonly string saveKey;
        private readonly DailyRewardSaveData save;

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
            save = datasave.Load<DailyRewardSaveData>(saveKey);
            ResetForChangedSchedule();
        }

        public DailyRewardViewState GetState()
        {
            Progress progress = GetProgress(clock.UtcNow.Date);
            return CreateState(progress);
        }

        public DailyRewardClaimResult Claim()
        {
            DateTime today = clock.UtcNow.Date;
            Progress progress = GetProgress(today);
            if (progress.IsCompleted)
            {
                return DailyRewardClaimResult.Completed();
            }

            if (!progress.CanClaim)
            {
                return DailyRewardClaimResult.NotAvailable();
            }

            DailyRewardEntry entry = schedule.Rewards[progress.NextRewardIndex];
            ResourceAmount reward = entry.Reward;
            ResourceGrantRequest request = new(CreateClaimId(progress, entry), reward);
            if (!resourceWallet.TryGrant(request))
            {
                return DailyRewardClaimResult.GrantFailed();
            }

            ApplyClaim(progress, today);
            datasave.Save(save, saveKey);
            return DailyRewardClaimResult.Claimed(reward);
        }

        private DailyRewardViewState CreateState(Progress progress)
        {
            List<DailyRewardItemViewState> items = new(schedule.Rewards.Count);
            for (int index = 0; index < schedule.Rewards.Count; index++)
            {
                DailyRewardItemStatus status = ResolveItemStatus(index, progress);
                items.Add(new DailyRewardItemViewState(schedule.Rewards[index], status));
            }

            return new DailyRewardViewState(items, progress.CanClaim, progress.NextClaimUtc);
        }

        private DailyRewardItemStatus ResolveItemStatus(int index, Progress progress)
        {
            if (progress.IsCompleted || index > progress.NextRewardIndex)
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

        private Progress GetProgress(DateTime today)
        {
            int nextIndex = save.NextRewardIndex;
            int cycle = save.Cycle;
            if (nextIndex < 0 || nextIndex > schedule.Rewards.Count)
            {
                nextIndex = 0;
                cycle++;
            }

            if (nextIndex == schedule.Rewards.Count)
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

            if (elapsedDays > 1 && schedule.ResetProgressAfterMissedDay)
            {
                nextIndex = 0;
                cycle++;
            }

            return new Progress(nextIndex, cycle, true, false, DateTime.MinValue);
        }

        private void ApplyClaim(Progress progress, DateTime today)
        {
            int nextIndex = progress.NextRewardIndex + 1;
            int cycle = progress.Cycle;
            if (nextIndex == schedule.Rewards.Count && schedule.CycleMode == DailyRewardCycleMode.Repeat)
            {
                nextIndex = 0;
                cycle++;
            }

            save.ScheduleId = schedule.ScheduleId;
            save.NextRewardIndex = nextIndex;
            save.Cycle = cycle;
            save.LastClaimUtcTicks = today.Ticks;
        }

        private void ResetForChangedSchedule()
        {
            if (string.Equals(save.ScheduleId, schedule.ScheduleId, StringComparison.Ordinal))
            {
                return;
            }

            save.ScheduleId = schedule.ScheduleId;
            save.NextRewardIndex = 0;
            save.Cycle = 0;
            save.LastClaimUtcTicks = 0;
            datasave.Save(save, saveKey);
        }

        private DateTime GetLastClaimDate()
        {
            return save.LastClaimUtcTicks <= 0
                ? DateTime.MinValue
                : new DateTime(save.LastClaimUtcTicks, DateTimeKind.Utc).Date;
        }

        private string CreateClaimId(Progress progress, DailyRewardEntry entry)
        {
            return $"{schedule.ScheduleId}:{progress.Cycle}:{entry.Id}";
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
