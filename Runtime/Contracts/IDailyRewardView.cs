using System;

namespace Dreamy.DailyReward
{
    public interface IDailyRewardView
    {
        event Action ClaimRequested;
        event Action CloseRequested;

        void Render(DailyRewardViewState state);
        void SetClaimInteractable(bool interactable);
        void ShowClaimResult(DailyRewardClaimResult result);
        void Close();
    }
}
