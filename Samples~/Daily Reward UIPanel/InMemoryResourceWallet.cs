using System.Collections.Generic;
using Dreamy.Economy;

namespace Dreamy.DailyReward.Samples
{
    public sealed class InMemoryResourceWallet : IResourceWallet
    {
        private readonly Dictionary<ResourceId, long> balances = new();
        private readonly HashSet<string> transactionIds = new();
        public bool TryGrant(ResourceGrantRequest request)
        {
            if (!transactionIds.Add(request.TransactionId)) return true;
            balances.TryGetValue(request.Resource.ResourceId, out long current);
            balances[request.Resource.ResourceId] = current + request.Resource.Amount;
            return true;
        }
        public long GetBalance(ResourceId resourceId) => balances.TryGetValue(resourceId, out long value) ? value : 0;
    }
}
