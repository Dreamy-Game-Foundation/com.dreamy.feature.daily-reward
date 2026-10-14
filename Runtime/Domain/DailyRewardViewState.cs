using System;
using System.Collections.Generic;

namespace Dreamy.DailyReward
{
    public sealed class DailyRewardViewState
    {
        public DailyRewardViewState(IReadOnlyList<DailyRewardItemViewState> rewards, bool canClaim, DateTime nextClaimUtc,
            bool hasPendingClaim = false, bool isBusy = false, bool migrationRequired = false)
        {
            Rewards = rewards ?? throw new ArgumentNullException(nameof(rewards));
            CanClaim = canClaim;
            NextClaimUtc = nextClaimUtc;
            HasPendingClaim = hasPendingClaim;
            IsBusy = isBusy;
            MigrationRequired = migrationRequired;
        }

        public IReadOnlyList<DailyRewardItemViewState> Rewards { get; }
        public bool CanClaim { get; }
        public DateTime NextClaimUtc { get; }
        public bool HasPendingClaim { get; }
        public bool IsBusy { get; }
        public bool MigrationRequired { get; }
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
