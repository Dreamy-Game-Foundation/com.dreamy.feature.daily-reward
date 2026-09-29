using System;
using System.Collections.Generic;

namespace Dreamy.DailyReward
{
    public sealed class DailyRewardViewState
    {
        public DailyRewardViewState(IReadOnlyList<DailyRewardItemViewState> rewards, bool canClaim, DateTime nextClaimUtc)
        {
            Rewards = rewards ?? throw new ArgumentNullException(nameof(rewards));
            CanClaim = canClaim;
            NextClaimUtc = nextClaimUtc;
        }

        public IReadOnlyList<DailyRewardItemViewState> Rewards { get; }
        public bool CanClaim { get; }
        public DateTime NextClaimUtc { get; }
    }

    public readonly struct DailyRewardItemViewState
    {
        public DailyRewardItemViewState(DailyRewardEntry entry, DailyRewardItemStatus status)
        {
            Entry = entry ?? throw new ArgumentNullException(nameof(entry));
            Status = status;
        }

        public DailyRewardEntry Entry { get; }
        public DailyRewardItemStatus Status { get; }
    }

    public enum DailyRewardItemStatus
    {
        Locked,
        Claimed,
        Claimable,
        Cooldown
    }
}
