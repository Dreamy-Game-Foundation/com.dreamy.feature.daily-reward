using Dreamy.Core;
using Dreamy.DailyReward;
using UnityEngine;

namespace Dreamy.Feature.DailyReward.Integration
{
    public sealed class DailyRewardController : MonoBehaviour
    {
        [SerializeField] private DailyRewardPanel panel;
        [SerializeField] private DailyRewardAudioFeedback feedback;
        private DailyRewardPresenter presenter;

        private void Start()
        {
            presenter = new DailyRewardPresenter(ServiceLocator.Get<IDailyRewardService>(), panel, feedback);
            presenter.Show();
        }

        private void OnDestroy() => presenter?.Dispose();
    }
}
