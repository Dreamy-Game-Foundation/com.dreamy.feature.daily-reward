using Dreamy.Economy;

namespace Dreamy.DailyReward
{
    public interface IDailyRewardFeedback
    {
        void PlayClaimed(ResourceAmount reward);
        void PlayClaimFailed();
    }
}
