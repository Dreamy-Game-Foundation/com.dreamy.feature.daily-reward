using System;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using Dreamy.DailyReward;
using Dreamy.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Dreamy.Feature.DailyReward.Integration
{
    public sealed class DailyRewardPanel : UIPanel, IDailyRewardView
    {
        [SerializeField] private Button claimButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Transform rewardContainer;
        [SerializeField] private DailyRewardRewardItem rewardItemPrefab;
        private readonly List<DailyRewardRewardItem> items = new();

        public override bool CanBack => true;
        public event Action ClaimRequested;
        public event Action CloseRequested;
        public event Action Destroyed;

        private void OnEnable()
        {
            claimButton.onClick.AddListener(RequestClaim);
            closeButton.onClick.AddListener(RequestClose);
        }

        protected override void OnDisable()
        {
            claimButton.onClick.RemoveListener(RequestClaim);
            closeButton.onClick.RemoveListener(RequestClose);
            base.OnDisable();
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
            Destroyed?.Invoke();
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
