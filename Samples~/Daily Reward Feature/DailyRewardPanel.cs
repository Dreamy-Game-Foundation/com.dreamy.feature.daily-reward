using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Dreamy.Feature;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Dreamy.Feature.DailyReward.Integration
{
    public sealed class DailyRewardPanel : FeaturePanel, IDailyRewardView
    {
        [SerializeField] private Button claimButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Transform rewardContainer;
        [SerializeField] private DailyRewardRewardItem rewardItemPrefab;
        private readonly List<DailyRewardRewardItem> items = new();

        public event Action ClaimRequested;
        public event Action CloseRequested;

        private void OnEnable()
        {
            claimButton.onClick.AddListener(RequestClaim);
            closeButton.onClick.AddListener(RequestClose);
        }

        private void OnDisable()
        {
            claimButton.onClick.RemoveListener(RequestClaim);
            closeButton.onClick.RemoveListener(RequestClose);
        }

        public void Render(DailyRewardViewState state)
        {
            EnsureItems(state.Rewards.Count);
            for (int index = 0; index < state.Rewards.Count; index++) items[index].Render(state.Rewards[index]);
            statusText.text = state.CanClaim ? "Reward available" : $"Next reward: {state.NextClaimUtc:yyyy-MM-dd}";
        }

        public void SetClaimInteractable(bool interactable) => claimButton.interactable = interactable;

        public void ShowClaimResult(DailyRewardClaimResult result) => statusText.text = result.Status.ToString();

        public void Close() => Hide().Forget();

        protected override void OnDestroy()
        {
            foreach (DailyRewardRewardItem item in items) if (item != null) Destroy(item.gameObject);
            base.OnDestroy();
        }

        private void EnsureItems(int count)
        {
            if (rewardItemPrefab == null || rewardContainer == null) throw new InvalidOperationException("Assign a reward item prefab and container.");
            while (items.Count < count) items.Add(Instantiate(rewardItemPrefab, rewardContainer));
        }

        private void RequestClaim() => ClaimRequested?.Invoke();
        private void RequestClose() => CloseRequested?.Invoke();
    }
}
