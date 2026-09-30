using System;
using System.Collections.Generic;
using Dreamy.Economy;

namespace Dreamy.Feature.DailyReward.Integration
{
    public sealed class InMemoryResourceWallet : IResourceWallet, IResourceBalanceSource
    {
        private readonly Dictionary<ResourceId, long> balances = new();
        private readonly HashSet<string> transactionIds = new();
        public event Action<ResourceBalanceChanged> BalanceChanged;
        public bool TryGrant(ResourceGrantRequest request)
        {
            if (!transactionIds.Add(request.TransactionId)) return true;
            balances.TryGetValue(request.Resource.ResourceId, out long current);
            balances[request.Resource.ResourceId] = current + request.Resource.Amount;
            Notify(request.Resource.ResourceId);
            return true;
        }

        public bool TryExchange(ResourceExchangeRequest request)
        {
            if (transactionIds.Contains(request.TransactionId)) return true;
            if (GetBalance(request.Cost.ResourceId) < request.Cost.Amount) return false;

            balances[request.Cost.ResourceId] = GetBalance(request.Cost.ResourceId) - request.Cost.Amount;
            Notify(request.Cost.ResourceId);
            foreach (ResourceAmount reward in request.Rewards)
            {
                balances[reward.ResourceId] = GetBalance(reward.ResourceId) + reward.Amount;
                Notify(reward.ResourceId);
            }

            transactionIds.Add(request.TransactionId);
            return true;
        }

        public long GetBalance(ResourceId resourceId) => balances.TryGetValue(resourceId, out long value) ? value : 0;
        private void Notify(ResourceId id) => BalanceChanged?.Invoke(new ResourceBalanceChanged(id, GetBalance(id)));
    }
}
