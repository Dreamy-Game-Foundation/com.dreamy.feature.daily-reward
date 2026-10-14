using Dreamy.Datasave;
using Dreamy.Economy.Rewards;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Dreamy.DailyReward
{
    public sealed class DailyRewardSaveData : SaveData
    {
        [JsonProperty("scheduleId")]
        public string ScheduleId { get; set; }

        [JsonProperty("nextRewardIndex")]
        public int NextRewardIndex { get; set; }

        [JsonProperty("cycle")]
        public int Cycle { get; set; }

        [JsonProperty("lastClaimUtcTicks")]
        public long LastClaimUtcTicks { get; set; }

        [JsonProperty("activeSchedule")]
        public DailyRewardScheduleConfig ActiveSchedule { get; set; }

        [JsonProperty("pendingClaim")]
        public DailyRewardPendingClaim PendingClaim { get; set; }

        [JsonProperty("journal")]
        public RewardJournal Journal { get; set; }

        public override int Version => 2;

        public DailyRewardSaveData Clone() => new()
        {
            ScheduleId = ScheduleId,
            NextRewardIndex = NextRewardIndex,
            Cycle = Cycle,
            LastClaimUtcTicks = LastClaimUtcTicks,
            ActiveSchedule = ActiveSchedule,
            PendingClaim = PendingClaim?.Clone(),
            Journal = Journal?.Clone()
        };
    }

    // Original claim context and intended transition share the journal's save document.
    public sealed class DailyRewardPendingClaim
    {
        public long ClaimUtcTicks { get; set; }
        public int ClaimCycle { get; set; }
        public string EntryId { get; set; }
        public string Revision { get; set; }
        public int NextRewardIndex { get; set; }
        public int NextCycle { get; set; }
        public int BundleSchemaVersion { get; set; }
        public string ResolverVersion { get; set; }
        public bool RequiresResolution { get; set; }
        public List<RewardJournalEntry> Entries { get; set; } = new();

        public ResolvedRewardBundle GetBundle()
        {
            var entries = new List<ResolvedRewardEntry>();
            foreach (RewardJournalEntry entry in Entries)
                entries.Add(new ResolvedRewardEntry(entry.EntryId, entry.KindId, entry.TargetId, entry.Amount));
            return new ResolvedRewardBundle(BundleSchemaVersion, ResolverVersion, entries);
        }

        public DailyRewardPendingClaim Clone()
        {
            var clone = new DailyRewardPendingClaim
            {
                ClaimUtcTicks = ClaimUtcTicks, ClaimCycle = ClaimCycle, EntryId = EntryId,
                Revision = Revision, NextRewardIndex = NextRewardIndex, NextCycle = NextCycle,
                BundleSchemaVersion = BundleSchemaVersion, ResolverVersion = ResolverVersion,
                RequiresResolution = RequiresResolution
            };
            foreach (RewardJournalEntry entry in Entries) clone.Entries.Add(entry.Clone());
            return clone;
        }
    }
}
