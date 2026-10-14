using Dreamy.Economy.Rewards;

namespace Dreamy.DailyReward
{
    // Called synchronously only after intent is durable. Implementations must replay
    // the same outcome for a request ID, including after a failed resolved save.
    public interface IDailyRewardBundleResolver
    {
        RewardDeliveryResult Resolve(string requestId, ResolvedRewardBundle original,
            out ResolvedRewardBundle resolved);
    }
}
