using System;
using Dreamy.Feature;
using Dreamy.UI;

namespace Dreamy.DailyReward
{
    public sealed class DailyRewardPresenter : FeaturePresenter, IPanelPresenter
    {
        private readonly IDailyRewardService service;
        private readonly IDailyRewardView view;
        private readonly IDailyRewardFeedback feedback;

        public DailyRewardPresenter(
            IDailyRewardService service,
            IDailyRewardView view,
            IDailyRewardFeedback feedback = null)
        {
            this.service = service ?? throw new ArgumentNullException(nameof(service));
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.feedback = feedback;
        }

        public override void Refresh()
        {
            DailyRewardViewState state = service.GetState();
            view.Render(state);
            view.SetClaimInteractable(state.CanClaim);
        }

        protected override void Unbind()
        {
            view.ClaimRequested -= Claim;
            view.CloseRequested -= Close;
        }

        protected override void Bind()
        {
            view.ClaimRequested += Claim;
            view.CloseRequested += Close;
        }

        private void Claim()
        {
            view.SetClaimInteractable(false);
            DailyRewardClaimResult result = service.Claim();
            if (result.IsSuccess)
            {
                if (result.Reward.HasValue) feedback?.PlayClaimed(result.Reward.Value);
                else if (result.Bundle != null && feedback is IDailyRewardBundleFeedback bundleFeedback)
                    bundleFeedback.PlayClaimed(result.Bundle);
            }
            else if (result.Status == DailyRewardClaimStatus.GrantFailed)
            {
                feedback?.PlayClaimFailed();
            }

            Refresh();
            // Keep the failure/retry reason visible after rendering the updated state.
            view.ShowClaimResult(result);
        }

        private void Close()
        {
            Dispose();
            view.Close();
        }
    }
}
