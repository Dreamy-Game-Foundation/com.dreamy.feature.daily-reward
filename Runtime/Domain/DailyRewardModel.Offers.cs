using System;
using System.Globalization;
using Dreamy.Economy.Rewards;

namespace Dreamy.DailyReward
{
    public sealed partial class DailyRewardModel
    {
        public DailyRewardClaimOffer GetClaimOffer(string token = null) => GetClaimOfferCore(token,null);
        public DailyRewardClaimResult ClaimOffer(string token,string expectedFingerprint)
        {
            if(string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(expectedFingerprint)) return DailyRewardClaimResult.NotAvailable();
            return ClaimCore(token,expectedFingerprint);
        }
        private DailyRewardClaimOffer GetClaimOfferCore(string token,DateTime? claimDate)
        {
            // Manual preview supports strict concrete bundles; unresolved mystery rewards are notice-only.
            if(gateway == null || RequiresMigration) return null;
            if(save.PendingClaim != null && save.Journal != null)
            {
                if(!save.Journal.IsValid()) return null;
                var original=save.PendingClaim;
                string originalToken=OfferToken(save.ActiveSchedule.ScheduleId,original.ClaimUtcTicks,original.ClaimCycle,original.EntryId,original.Revision);
                if((token == originalToken || (token == null && HasPendingClaim)) && !original.RequiresResolution)
                {
                    var bundle=save.Journal.State == RewardJournalState.IntentPersisted ? original.GetBundle() : save.Journal.GetResolvedBundle();
                    return new DailyRewardClaimOffer(originalToken,bundle,HasPendingClaim,save.Journal.State == RewardJournalState.Completed);
                }
                if(HasPendingClaim) return null;
            }
            var today=claimDate ?? clock.UtcNow.Date;
            var active=GetActiveSchedule();var progress=GetProgress(today,active);
            if(!progress.CanClaim || progress.IsCompleted) return null;
            var entry=active.Rewards[progress.NextRewardIndex];if(entry.RequiresResolution)return null;
            string current=OfferToken(active.ScheduleId,today.Ticks,progress.Cycle,entry.Id,active.Revision);
            if(token != null && token != current) return null;
            return new DailyRewardClaimOffer(current,entry.GetBundle(),false,false);
        }
        private string OfferToken(string scheduleId,long date,int cycle,string entryId,string revision) =>
            RewardIdentity.Encode("daily-offer-v1",saveKey,scheduleId,date.ToString(CultureInfo.InvariantCulture),
                cycle.ToString(CultureInfo.InvariantCulture),entryId,revision ?? string.Empty);
    }
}
