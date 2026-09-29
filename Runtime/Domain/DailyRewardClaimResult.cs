using Dreamy.Economy;

namespace Dreamy.DailyReward
{
    public readonly struct DailyRewardClaimResult
    {
        private DailyRewardClaimResult(DailyRewardClaimStatus status, ResourceAmount? reward)
        {
            Status = status;
            Reward = reward;
        }

        public DailyRewardClaimStatus Status { get; }
        public ResourceAmount? Reward { get; }
        public bool IsSuccess => Status == DailyRewardClaimStatus.Claimed;

        public static DailyRewardClaimResult Claimed(ResourceAmount reward) =>
            new(DailyRewardClaimStatus.Claimed, reward);

        public static DailyRewardClaimResult NotAvailable() =>
            new(DailyRewardClaimStatus.NotAvailable, null);

        public static DailyRewardClaimResult GrantFailed() =>
            new(DailyRewardClaimStatus.GrantFailed, null);

        public static DailyRewardClaimResult Completed() =>
            new(DailyRewardClaimStatus.Completed, null);
    }

    public enum DailyRewardClaimStatus
    {
        Claimed,
        NotAvailable,
        GrantFailed,
        Completed
    }
}
