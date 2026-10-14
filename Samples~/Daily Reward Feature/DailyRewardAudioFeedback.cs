using Dreamy.Audio;
using Dreamy.DailyReward;
using Dreamy.Economy;
using Dreamy.Economy.Rewards;
using UnityEngine;

namespace Dreamy.Feature.DailyReward.Integration
{
    public sealed class DailyRewardAudioFeedback : MonoBehaviour, IDailyRewardFeedback, IDailyRewardBundleFeedback
    {
        [SerializeField] private string claimedAudioId;
        [SerializeField] private string failedAudioId;
        public void PlayClaimed(ResourceAmount reward) => DreamyAudio.Play(claimedAudioId);
        public void PlayClaimed(ResolvedRewardBundle bundle) => DreamyAudio.Play(claimedAudioId);
        public void PlayClaimFailed() => DreamyAudio.Play(failedAudioId);
    }
}
