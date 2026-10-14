using Dreamy.Economy;
using Dreamy.Economy.Rewards;

namespace Dreamy.DailyReward
{
    public interface IDailyRewardFeedback
    {
        void PlayClaimed(ResourceAmount reward);
        void PlayClaimFailed();
    }

    // Optional bundle capability; existing feedback implementations remain valid.
    public interface IDailyRewardBundleFeedback
    {
        void PlayClaimed(ResolvedRewardBundle bundle);
    }
}
