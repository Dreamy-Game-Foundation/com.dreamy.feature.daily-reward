namespace Dreamy.DailyReward
{
    public interface IDailyRewardService
    {
        DailyRewardViewState GetState();
        DailyRewardClaimResult Claim();
    }
}
