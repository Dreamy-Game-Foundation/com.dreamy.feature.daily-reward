using Dreamy.Economy;
using Dreamy.Economy.Rewards;

namespace Dreamy.DailyReward
{
    public readonly struct DailyRewardClaimResult
    {
        private DailyRewardClaimResult(DailyRewardClaimStatus status, ResourceAmount? reward,
            ResolvedRewardBundle bundle = null, RewardDeliveryReason reason = RewardDeliveryReason.None, bool retryable = false)
        {
            Status = status;
            Reward = reward;
            Bundle = bundle;
            Reason = reason;
            Retryable = retryable;
        }

        public DailyRewardClaimStatus Status { get; }
        public ResourceAmount? Reward { get; }
        public ResolvedRewardBundle Bundle { get; }
        public RewardDeliveryReason Reason { get; }
        public bool Retryable { get; }
        public bool IsSuccess => Status == DailyRewardClaimStatus.Claimed;

        public static DailyRewardClaimResult Claimed(ResourceAmount reward) =>
            new(DailyRewardClaimStatus.Claimed, reward);

        public static DailyRewardClaimResult Claimed(ResolvedRewardBundle bundle, ResourceAmount? reward = null) =>
            new(DailyRewardClaimStatus.Claimed, reward, bundle);

        public static DailyRewardClaimResult Delivery(RewardDeliveryResult result) =>
            new(result.Status == RewardDeliveryStatus.Pending ? DailyRewardClaimStatus.Pending : DailyRewardClaimStatus.GrantFailed,
                null, null, result.Reason, result.Retryable);

        public static DailyRewardClaimResult Busy() =>
            new(DailyRewardClaimStatus.Busy, null, null, RewardDeliveryReason.Busy, true);

        public static DailyRewardClaimResult MigrationRequired() =>
            new(DailyRewardClaimStatus.MigrationRequired, null);

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
        Completed,
        Pending,
        Busy,
        MigrationRequired
    }
}
