using Dreamy.DailyReward;
using TMPro;
using UnityEngine;

namespace Dreamy.Feature.DailyReward.Integration
{
    public sealed class DailyRewardRewardItem : MonoBehaviour
    {
        [SerializeField] private TMP_Text dayText;
        [SerializeField] private TMP_Text amountText;
        [SerializeField] private TMP_Text stateText;

        public void Render(DailyRewardItemViewState state)
        {
            dayText.text = $"Day {state.Entry.Day}";
            amountText.text = $"{state.Entry.Reward.Amount} {state.Entry.Reward.ResourceId}";
            stateText.text = state.Status.ToString();
        }
    }
}
