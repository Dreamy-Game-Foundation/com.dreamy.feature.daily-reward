using System;
using Dreamy.Feature;

namespace Dreamy.DailyReward
{
    public interface IDailyRewardView : IFeatureView<DailyRewardViewState>
    {
        event Action ClaimRequested;
        event Action CloseRequested;

        void SetClaimInteractable(bool interactable);
        void ShowClaimResult(DailyRewardClaimResult result);
        void Close();
    }
}
