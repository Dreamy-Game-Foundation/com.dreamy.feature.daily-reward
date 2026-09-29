using Dreamy.Audio;
using Dreamy.Economy;
using UnityEngine;

namespace Dreamy.DailyReward.Samples
{
    public sealed class DailyRewardAudioFeedback : MonoBehaviour, IDailyRewardFeedback
    {
        [SerializeField] private string claimedAudioId;
        [SerializeField] private string failedAudioId;
        public void PlayClaimed(ResourceAmount reward) => DreamyAudio.Play(claimedAudioId);
        public void PlayClaimFailed() => DreamyAudio.Play(failedAudioId);
    }
}
