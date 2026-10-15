using System;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using Dreamy.DailyReward;
using Dreamy.UI;
using Dreamy.Economy;
using Dreamy.Economy.UI;
using Dreamy.Feedback;
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
        [Header("Confirmed claim feedback")]
        [SerializeField] private ResourceUIHolder[] rewardHolders = Array.Empty<ResourceUIHolder>();
        [SerializeField] private FeedbackPlayer rewardFeedback;
        private int presentationVersion;
        private bool presenting;
        private bool canClaim;
        private Transform claimableSource;
        private Transform rewardSource;
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
            presentationVersion++;
            presenting = false;
            rewardFeedback?.StopLast();
            base.OnDisable();
        }

        public void Render(DailyRewardViewState state)
        {
            EnsureItems(state.Rewards.Count);
            claimableSource = null;
            for (int index = 0; index < state.Rewards.Count; index++)
                if (state.Rewards[index].Status == DailyRewardItemStatus.Claimable) claimableSource = items[index].transform;
            for (int index = 0; index < state.Rewards.Count; index++) items[index].Render(state.Rewards[index]);
            for (int index = 0; index < items.Count; index++) items[index].gameObject.SetActive(index < state.Rewards.Count);
            statusText.text = state.MigrationRequired ? "Schedule migration required"
                : state.HasPendingClaim ? "Delivery pending - claim to retry"
                : state.IsBusy ? "Claim in progress"
                : state.IsCompleted ? "All rewards claimed"
                : state.CanClaim ? "Reward available" : $"Next reward: {state.NextClaimUtc:yyyy-MM-dd}";
        }

        public void SetClaimInteractable(bool interactable)
        {
            canClaim = interactable;
            claimButton.interactable = interactable && !presenting;
        }

        public void BindFeedbackHost(FeedbackHost host)
        {
            if (rewardFeedback != null && host != null) rewardFeedback.BindHost(host);
        }

        public void ShowClaimResult(DailyRewardClaimResult result)
        {
            if (result.IsSuccess && isActiveAndEnabled)
                PresentClaimAsync(result).Forget();
            else ShowResult(result);
        }

        private void ShowResult(DailyRewardClaimResult result) =>
            statusText.text = result.Reason == Dreamy.Economy.Rewards.RewardDeliveryReason.None
                ? result.Status.ToString()
                : $"{result.Status}: {result.Reason}{(result.Retryable ? " - claim to retry" : "")}";

        private async UniTask PresentClaimAsync(DailyRewardClaimResult result)
        {
            int version = ++presentationVersion;
            presenting = true;
            claimButton.interactable = false;
            statusText.text = "Collecting reward...";
            try
            {
                // A resolved bundle is authoritative; Reward may also contain its legacy currency entry.
                if (result.Bundle != null)
                {
                    foreach (var entry in result.Bundle.Entries)
                    {
                        if (entry.KindId != DatasaveResourceWallet.CurrencyKind ||
                            !ResourceId.TryParse(entry.TargetId, out var resource)) continue;
                        await PresentResourceAsync(resource, entry.Amount, version);
                        if (version != presentationVersion || !isActiveAndEnabled) return;
                    }
                }
                else if (result.Reward.HasValue)
                    await PresentResourceAsync(result.Reward.Value.ResourceId, result.Reward.Value.Amount, version);
            }
            finally
            {
                if (version == presentationVersion && isActiveAndEnabled)
                {
                    presenting = false;
                    foreach (var holder in rewardHolders) if (holder != null) holder.RefreshBalance();
                    claimButton.interactable = canClaim;
                    ShowResult(result);
                }
            }
        }

        private async UniTask PresentResourceAsync(ResourceId resource, long amount, int version)
        {
            foreach (var holder in rewardHolders)
            {
                if (holder == null || holder.ResourceId != resource) continue;
                if (rewardFeedback != null)
                {
                    var handle = rewardFeedback.PlayBetween(rewardSource != null ? rewardSource : claimButton.transform,
                        holder.IconTarget, amount, holder.Icon);
                    await handle.WaitAsync();
                    if (version != presentationVersion || !isActiveAndEnabled || handle.Status == FeedbackStatus.Stopped) return;
                }
                holder.RefreshBalance();
            }
        }

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

        private void RequestClaim()
        {
            if (presenting) return;
            rewardSource = claimableSource;
            ClaimRequested?.Invoke();
        }
        private void RequestClose() => CloseRequested?.Invoke();
    }
}
