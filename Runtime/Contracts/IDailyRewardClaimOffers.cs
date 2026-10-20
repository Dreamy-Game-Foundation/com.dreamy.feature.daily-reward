using Dreamy.Economy.Rewards;

namespace Dreamy.DailyReward
{
    // Optional identity-bound capability. Existing IDailyRewardService callers remain unchanged.
    public interface IDailyRewardClaimOffers
    {
        DailyRewardClaimOffer GetClaimOffer(string token = null);
        DailyRewardClaimResult ClaimOffer(string token, string expectedFingerprint);
    }
    public sealed class DailyRewardClaimOffer
    {
        internal DailyRewardClaimOffer(string token, ResolvedRewardBundle bundle, bool pending, bool completed)
        { Token=token; Bundle=bundle; IsPending=pending; IsCompleted=completed; Fingerprint=RewardIdentity.Fingerprint(token,bundle.Fingerprint); }
        public string Token { get; }
        public string Fingerprint { get; }
        public ResolvedRewardBundle Bundle { get; }
        public bool IsPending { get; }
        public bool IsCompleted { get; }
    }
}
