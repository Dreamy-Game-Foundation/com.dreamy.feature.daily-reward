using Dreamy.Datasave;
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

        public override int Version => 1;
    }
}
