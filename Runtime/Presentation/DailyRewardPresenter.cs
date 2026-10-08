using System;
using Dreamy.UI;

namespace Dreamy.DailyReward
{
    public sealed class DailyRewardPresenter : IPanelPresenter
    {
        private readonly IDailyRewardService service;
        private readonly IDailyRewardView view;
        private readonly IDailyRewardFeedback feedback;
        private bool isBound;

        public DailyRewardPresenter(
            IDailyRewardService service,
            IDailyRewardView view,
            IDailyRewardFeedback feedback = null)
        {
            this.service = service ?? throw new ArgumentNullException(nameof(service));
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.feedback = feedback;
        }

        public void Show()
        {
            Bind();
            Refresh();
        }

        public void Refresh()
        {
            DailyRewardViewState state = service.GetState();
            view.Render(state);
            view.SetClaimInteractable(state.CanClaim);
        }

        public void Dispose()
        {
            if (!isBound)
            {
                return;
            }

            view.ClaimRequested -= Claim;
            view.CloseRequested -= Close;
            isBound = false;
        }

        private void Bind()
        {
            if (isBound)
            {
                return;
            }

            view.ClaimRequested += Claim;
            view.CloseRequested += Close;
            isBound = true;
        }

        private void Claim()
        {
            view.SetClaimInteractable(false);
            DailyRewardClaimResult result = service.Claim();
            view.ShowClaimResult(result);
            if (result.IsSuccess)
            {
                feedback?.PlayClaimed(result.Reward.Value);
            }
            else if (result.Status == DailyRewardClaimStatus.GrantFailed)
            {
                feedback?.PlayClaimFailed();
            }

            Refresh();
        }

        private void Close()
        {
            Dispose();
            view.Close();
        }
    }
}
