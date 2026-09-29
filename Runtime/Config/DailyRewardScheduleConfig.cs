using System;
using System.Collections.Generic;
using Dreamy.DataConfig;
using Dreamy.Economy;
using Newtonsoft.Json;

namespace Dreamy.DailyReward
{
    public sealed class DailyRewardScheduleConfig : ConfigBase
    {
        [JsonProperty("scheduleId", Required = Required.Always)]
        private string scheduleId;

        [JsonProperty("cycleMode", Required = Required.Always)]
        private DailyRewardCycleMode cycleMode;

        [JsonProperty("resetProgressAfterMissedDay")]
        private bool resetProgressAfterMissedDay = true;

        [JsonProperty("rewards", Required = Required.Always)]
        private List<DailyRewardEntry> rewards = new();

        [JsonIgnore]
        public string ScheduleId => scheduleId;

        [JsonIgnore]
        public DailyRewardCycleMode CycleMode => cycleMode;

        [JsonIgnore]
        public bool ResetProgressAfterMissedDay => resetProgressAfterMissedDay;

        [JsonIgnore]
        public IReadOnlyList<DailyRewardEntry> Rewards => rewards;

        public override void Initialize(string documentName)
        {
            if (string.IsNullOrWhiteSpace(scheduleId))
            {
                throw new DataConfigException(documentName, "scheduleId cannot be empty.");
            }

            if (rewards == null || rewards.Count == 0)
            {
                throw new DataConfigException(documentName, "rewards must contain at least one entry.");
            }

            HashSet<string> ids = new(StringComparer.Ordinal);
            for (int index = 0; index < rewards.Count; index++)
            {
                DailyRewardEntry entry = rewards[index];
                if (entry == null)
                {
                    throw new DataConfigException(documentName, $"Reward at index {index} is null.");
                }

                entry.Validate(documentName, index, ids);
            }
        }
    }

    public enum DailyRewardCycleMode
    {
        StopAfterLastDay,
        Repeat
    }

    [Serializable]
    public sealed class DailyRewardEntry
    {
        [JsonProperty("id", Required = Required.Always)]
        private string id;

        [JsonProperty("day", Required = Required.Always)]
        private int day;

        [JsonProperty("resourceId", Required = Required.Always)]
        private string resourceId;

        [JsonProperty("amount", Required = Required.Always)]
        private long amount;

        [JsonProperty("isMilestone")]
        private bool isMilestone;

        [JsonIgnore]
        public string Id => id;

        [JsonIgnore]
        public int Day => day;

        [JsonIgnore]
        public ResourceAmount Reward => new(new ResourceId(resourceId), amount);

        [JsonIgnore]
        public bool IsMilestone => isMilestone;

        internal void Validate(string documentName, int index, ISet<string> ids)
        {
            if (string.IsNullOrWhiteSpace(id) || !ids.Add(id))
            {
                throw new DataConfigException(documentName, $"Reward at index {index} has a duplicate or empty id.");
            }

            if (day != index + 1)
            {
                throw new DataConfigException(documentName, "Reward days must start at 1 and be consecutive.");
            }

            if (!ResourceId.TryParse(resourceId, out _) || amount <= 0)
            {
                throw new DataConfigException(documentName, $"Reward '{id}' has invalid reward data.");
            }
        }
    }
}
