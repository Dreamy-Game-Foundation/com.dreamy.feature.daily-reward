using Dreamy.Core;
using UnityEngine;

namespace Dreamy.DailyReward.Samples
{
    public sealed class DailyRewardSampleController : MonoBehaviour
    {
        [SerializeField] private DailyRewardSamplePanel panel;
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
