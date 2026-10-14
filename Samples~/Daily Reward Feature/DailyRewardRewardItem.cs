using Dreamy.DailyReward;
using System.Text;
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
            var text = new StringBuilder();
            foreach (var reward in state.Entry.GetBundle().Entries)
            {
                if (text.Length > 0) text.Append('\n');
                text.Append(reward.Amount).Append(' ').Append(reward.TargetId);
            }
            amountText.text = text.ToString();
            stateText.text = state.Status.ToString();
        }
    }
}
