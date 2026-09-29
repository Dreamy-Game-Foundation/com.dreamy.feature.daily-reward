using System;

namespace Dreamy.DailyReward
{
    public sealed class SystemRewardClock : IRewardClock
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }
}
