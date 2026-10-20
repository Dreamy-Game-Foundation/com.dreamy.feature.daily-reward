using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Dreamy.Economy.UI;
using Dreamy.Economy.Rewards;

namespace Dreamy.DailyReward.Presentation
{
    public sealed class DailyRewardClaimSource : IRewardClaimSource
    {
        private readonly string scope;
        private readonly IDailyRewardClaimOffers offers;
        private bool requiresReload;
        public DailyRewardClaimSource(string scope,IDailyRewardClaimOffers offers)
        { this.scope=scope ?? throw new ArgumentNullException(nameof(scope));this.offers=offers ?? throw new ArgumentNullException(nameof(offers)); }
        public RewardClaimReference CurrentReference()
        { var offer=offers.GetClaimOffer();return offer == null ? null : new RewardClaimReference(scope,"daily",offer.Token); }
        private RewardClaimSnapshot Query(RewardClaimReference reference)
        {
            if(reference.Scope != scope || reference.SourceId != "daily")return new RewardClaimSnapshot(reference,"missing",null,RewardClaimStatus.Unavailable,"WrongScope");
            var offer=offers.GetClaimOffer(reference.ClaimId);
            return offer == null ? new RewardClaimSnapshot(reference,"missing",null,RewardClaimStatus.Expired,"OriginalClaimUnavailable") :
                new RewardClaimSnapshot(reference,offer.Fingerprint,offer.Bundle,requiresReload ? RewardClaimStatus.RequiresReload : offer.IsCompleted ? RewardClaimStatus.AlreadyClaimed : offer.IsPending ? RewardClaimStatus.Pending : RewardClaimStatus.Available,
                    requiresReload ? "Reconstruct source and affected owners from durable state" : null,offer.IsPending && !requiresReload);
        }
        public UniTask<RewardClaimSnapshot> QueryAsync(RewardClaimReference reference,CancellationToken token)
        { token.ThrowIfCancellationRequested();return UniTask.FromResult(Query(reference)); }
        public UniTask<RewardClaimSnapshot> ClaimAsync(RewardClaimCommand command)
        {
            var before=Query(command.Reference);
            if(before.Fingerprint != command.ExpectedFingerprint)return UniTask.FromResult(new RewardClaimSnapshot(command.Reference,command.ExpectedFingerprint,before.Bundle,RewardClaimStatus.Blocked,"PayloadMismatch"));
            if(before.IsCompleted || requiresReload || before.Status == RewardClaimStatus.Expired || before.Status == RewardClaimStatus.Unavailable)return UniTask.FromResult(before);
            DailyRewardClaimResult result;
            try { result=offers.ClaimOffer(command.Reference.ClaimId,command.ExpectedFingerprint); }
            catch { requiresReload=true;throw; }
            if(result.Reason == RewardDeliveryReason.PersistenceFailed)requiresReload=true;
            if(result.IsSuccess)return UniTask.FromResult(new RewardClaimSnapshot(command.Reference,before.Fingerprint,result.Bundle ?? before.Bundle,RewardClaimStatus.Claimed));
            var after=Query(command.Reference);
            if(after.IsCompleted)return UniTask.FromResult(after);
            return UniTask.FromResult(new RewardClaimSnapshot(command.Reference,before.Fingerprint,before.Bundle,
                requiresReload ? RewardClaimStatus.RequiresReload : result.Status == DailyRewardClaimStatus.Pending ? RewardClaimStatus.Pending : result.Status == DailyRewardClaimStatus.Busy ? RewardClaimStatus.Busy : RewardClaimStatus.Blocked,
                result.Status+":"+result.Reason,result.Retryable));
        }
    }
}
