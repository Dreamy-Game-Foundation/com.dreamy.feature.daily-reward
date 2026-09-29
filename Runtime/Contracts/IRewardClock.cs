using System;

namespace Dreamy.DailyReward
{
    public interface IRewardClock
    {
        DateTime UtcNow { get; }
    }
}
