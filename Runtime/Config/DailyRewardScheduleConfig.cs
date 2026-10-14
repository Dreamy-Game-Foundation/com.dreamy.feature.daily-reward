using System;
using System.Collections.Generic;
using System.Globalization;
using Dreamy.DataConfig;
using Dreamy.Economy;
using Dreamy.Economy.Rewards;
using Newtonsoft.Json;

namespace Dreamy.DailyReward
{
    public sealed class DailyRewardScheduleConfig : ConfigBase
    {
        [JsonProperty("scheduleId", Required = Required.Always)]
        private string scheduleId;

        [JsonProperty("revision")]
        private string revision = "1";

        [JsonProperty("cycleMode", Required = Required.Always)]
        private DailyRewardCycleMode cycleMode;

        [JsonProperty("resetProgressAfterMissedDay")]
        private bool resetProgressAfterMissedDay = true;

        [JsonProperty("rewards", Required = Required.Always)]
        private List<DailyRewardEntry> rewards = new();

        [JsonIgnore]
        public string ScheduleId => scheduleId;

        [JsonIgnore]
        public string Revision => revision;

        [JsonIgnore]
        public string Fingerprint
        {
            get
            {
                var fields = new List<string> { scheduleId, cycleMode.ToString(), resetProgressAfterMissedDay.ToString() };
                foreach (DailyRewardEntry entry in rewards)
                {
                    fields.Add(entry.Id);
                    fields.Add(entry.Day.ToString(CultureInfo.InvariantCulture));
                    fields.Add(entry.IsMilestone.ToString());
                    fields.Add(entry.RequiresResolution.ToString());
                    fields.Add(entry.GetBundle().Fingerprint);
                }
                return RewardIdentity.Fingerprint(fields.ToArray());
            }
        }

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

            if (SchemaVersion != 1)
                throw new DataConfigException(documentName, "Schedule schemaVersion must be 1.");
            if (string.IsNullOrWhiteSpace(revision) || !Enum.IsDefined(typeof(DailyRewardCycleMode), cycleMode))
                throw new DataConfigException(documentName, "Revision and cycle mode must be valid.");

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

        [JsonProperty("resourceId")]
        private string resourceId;

        [JsonProperty("amount")]
        private long? amount;

        [JsonProperty("bundle")]
        private DailyRewardBundleConfig bundle;

        [JsonProperty("isMilestone")]
        private bool isMilestone;

        [JsonIgnore]
        public string Id => id;

        [JsonIgnore]
        public int Day => day;

        [JsonIgnore]
        public ResourceAmount Reward => !IsBundle
            ? new ResourceAmount(new ResourceId(resourceId), amount.Value)
            : throw new InvalidOperationException("Bundled rewards do not have a single ResourceAmount.");

        [JsonIgnore]
        public bool IsBundle => bundle != null;

        [JsonIgnore]
        public bool RequiresResolution => bundle != null && bundle.RequiresResolution;

        public ResolvedRewardBundle GetBundle() => bundle != null
            ? bundle.Resolve()
            : new ResolvedRewardBundle(ResolvedRewardBundle.CurrentSchemaVersion, "daily-legacy-v1",
                new[] { new ResolvedRewardEntry(id, "currency", resourceId, amount.Value) });

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

            if (bundle != null)
            {
                if (resourceId != null || amount.HasValue)
                    throw new DataConfigException(documentName, $"Reward '{id}' declares both legacy and bundle data.");
                try { bundle.Resolve(); }
                catch (ArgumentException exception)
                {
                    throw new DataConfigException(documentName, $"Reward '{id}' has invalid bundle data: {exception.Message}");
                }
                return;
            }

            if (!ResourceId.TryParse(resourceId, out _) || !amount.HasValue || amount.Value <= 0)
            {
                throw new DataConfigException(documentName, $"Reward '{id}' has invalid reward data.");
            }
        }
    }

    [Serializable]
    public sealed class DailyRewardBundleConfig
    {
        [JsonProperty("requiresResolution")]
        public bool RequiresResolution { get; private set; }

        [JsonProperty("schemaVersion", Required = Required.Always)]
        private int schemaVersion;

        [JsonProperty("resolverVersion", Required = Required.Always)]
        private string resolverVersion;

        [JsonProperty("entries", Required = Required.Always)]
        private List<DailyRewardBundleEntryConfig> entries;

        public ResolvedRewardBundle Resolve()
        {
            if (entries == null) throw new ArgumentException("Bundle entries are required.");
            var resolved = new List<ResolvedRewardEntry>(entries.Count);
            foreach (DailyRewardBundleEntryConfig entry in entries)
            {
                if (entry == null) throw new ArgumentException("Bundle entries cannot be null.");
                resolved.Add(entry.Resolve());
            }
            return new ResolvedRewardBundle(schemaVersion, resolverVersion, resolved);
        }
    }

    [Serializable]
    public sealed class DailyRewardBundleEntryConfig
    {
        [JsonProperty("entryId", Required = Required.Always)]
        private string entryId;
        [JsonProperty("kindId", Required = Required.Always)]
        private string kindId;
        [JsonProperty("targetId", Required = Required.Always)]
        private string targetId;
        [JsonProperty("amount", Required = Required.Always)]
        private long amount;

        internal ResolvedRewardEntry Resolve() => new(entryId, kindId, targetId, amount);
    }
}
