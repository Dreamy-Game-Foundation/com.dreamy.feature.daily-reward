using System;
using System.Collections.Generic;
using Dreamy.Datasave;
using Dreamy.Economy.Rewards;

namespace Dreamy.Feature.DailyReward.Integration
{
    // Demo only: one known concrete item. Mutation and receipt share one owner save.
    public sealed class DemoItemDestination : IRewardDestination
    {
        private readonly IDatasaveService save;
        private readonly string saveKey;
        private DemoItemSaveData data;

        public DemoItemDestination(IDatasaveService save, string saveKey = "daily-demo-items")
        {
            this.save = save ?? throw new ArgumentNullException(nameof(save));
            this.saveKey = saveKey;
            data = save.Load<DemoItemSaveData>(saveKey);
        }

        public string KindId => "item";
        public long Count => data.Count;
        public bool SimulateUnavailable { get; set; }

        public RewardDeliveryResult Validate(IReadOnlyList<RewardDeliveryRequest> requests)
        {
            long count = data.Count;
            foreach (RewardDeliveryRequest request in requests)
            {
                if (!request.IsValid || request.Entry.KindId != KindId || request.Entry.TargetId != "item.demo-card")
                    return RewardDeliveryResult.Failed(RewardDeliveryReason.UnknownTarget);
                if (data.Receipts.TryGetValue(request.TransactionId, out string fingerprint))
                {
                    if (fingerprint != request.Fingerprint) return RewardDeliveryResult.Failed(RewardDeliveryReason.PayloadMismatch);
                    continue;
                }
                try { count = checked(count + request.Entry.Amount); }
                catch (OverflowException) { return RewardDeliveryResult.Failed(RewardDeliveryReason.Overflow); }
            }
            return RewardDeliveryResult.Delivered();
        }

        public RewardDeliveryResult Deliver(RewardDeliveryRequest request)
        {
            if (SimulateUnavailable) return RewardDeliveryResult.Pending(RewardDeliveryReason.DestinationUnavailable);
            RewardDeliveryResult validation = Validate(new[] { request });
            if (!validation.IsSuccess) return validation;
            if (data.Receipts.ContainsKey(request.TransactionId)) return RewardDeliveryResult.AlreadyDelivered();
            var candidate = new DemoItemSaveData
            {
                Count = checked(data.Count + request.Entry.Amount),
                Receipts = new Dictionary<string, string>(data.Receipts, StringComparer.Ordinal)
            };
            candidate.Receipts.Add(request.TransactionId, request.Fingerprint);
            try { save.Save(candidate, saveKey); }
            catch (Exception) { return RewardDeliveryResult.Pending(RewardDeliveryReason.PersistenceFailed); }
            data = candidate;
            return RewardDeliveryResult.Delivered();
        }
    }

    public sealed class DemoItemSaveData : SaveData
    {
        public long Count { get; set; }
        public Dictionary<string, string> Receipts { get; set; } = new(StringComparer.Ordinal);
    }
}
