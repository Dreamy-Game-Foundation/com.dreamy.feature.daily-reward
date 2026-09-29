using System;
using System.Collections.Generic;
using Dreamy.Datasave;
using Dreamy.Economy;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Dreamy.DailyReward.Tests
{
    public sealed class DailyRewardModelTests
    {
        [Test]
        public void Claim_WhenFirstRewardIsAvailable_GrantsRewardAndStartsCooldown()
        {
            var clock = new FakeClock(new DateTime(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc));
            var grants = new FakeResourceWallet();
            DailyRewardModel model = CreateModel(clock, grants, out _);

            DailyRewardClaimResult result = model.Claim();

            Assert.That(result.Status, Is.EqualTo(DailyRewardClaimStatus.Claimed));
            Assert.That(grants.Requests, Has.Count.EqualTo(1));
            Assert.That(grants.Requests[0].TransactionId, Is.EqualTo("test-schedule:0:day-1"));
            Assert.That(model.GetState().CanClaim, Is.False);
            Assert.That(model.GetState().Rewards[0].Status, Is.EqualTo(DailyRewardItemStatus.Claimed));
            Assert.That(model.GetState().Rewards[1].Status, Is.EqualTo(DailyRewardItemStatus.Cooldown));
        }

        [Test]
        public void Claim_WhenGrantFails_DoesNotAdvanceSavedProgress()
        {
            var clock = new FakeClock(new DateTime(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc));
            var grants = new FakeResourceWallet { ShouldSucceed = false };
            DailyRewardModel model = CreateModel(clock, grants, out InMemoryDatasaveService datasave);

            DailyRewardClaimResult result = model.Claim();

            Assert.That(result.Status, Is.EqualTo(DailyRewardClaimStatus.GrantFailed));
            Assert.That(model.GetState().CanClaim, Is.True);
            Assert.That(datasave.SaveCount, Is.EqualTo(1));
        }

        [Test]
        public void GetState_WhenPlayerMissesADay_ResetsToFirstReward()
        {
            var clock = new FakeClock(new DateTime(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc));
            var grants = new FakeResourceWallet();
            DailyRewardModel model = CreateModel(clock, grants, out _);
            model.Claim();
            clock.UtcNow = clock.UtcNow.AddDays(2);

            DailyRewardViewState state = model.GetState();

            Assert.That(state.CanClaim, Is.True);
            Assert.That(state.Rewards[0].Status, Is.EqualTo(DailyRewardItemStatus.Claimable));
            Assert.That(state.Rewards[1].Status, Is.EqualTo(DailyRewardItemStatus.Locked));
        }

        [Test]
        public void Claim_WhenRepeatScheduleFinishes_StartsNewCycleWithDistinctClaimId()
        {
            var clock = new FakeClock(new DateTime(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc));
            var grants = new FakeResourceWallet();
            DailyRewardModel model = CreateModel(clock, grants, out _);
            model.Claim();
            clock.UtcNow = clock.UtcNow.AddDays(1);
            model.Claim();
            clock.UtcNow = clock.UtcNow.AddDays(1);

            DailyRewardClaimResult result = model.Claim();

            Assert.That(result.Status, Is.EqualTo(DailyRewardClaimStatus.Claimed));
            Assert.That(grants.Requests[2].TransactionId, Is.EqualTo("test-schedule:1:day-1"));
        }

        private static DailyRewardModel CreateModel(
            FakeClock clock,
            FakeResourceWallet grants,
            out InMemoryDatasaveService datasave)
        {
            DailyRewardScheduleConfig schedule = JsonConvert.DeserializeObject<DailyRewardScheduleConfig>(
                "{\"scheduleId\":\"test-schedule\",\"cycleMode\":\"Repeat\",\"resetProgressAfterMissedDay\":true,\"rewards\":[{\"id\":\"day-1\",\"day\":1,\"resourceId\":\"currency.gold\",\"amount\":100},{\"id\":\"day-2\",\"day\":2,\"resourceId\":\"currency.gems\",\"amount\":5}]}" );
            schedule.Initialize("test");
            datasave = new InMemoryDatasaveService();
            return new DailyRewardModel(schedule, datasave, grants, clock, "test-save");
        }

        private sealed class FakeClock : IRewardClock
        {
            public FakeClock(DateTime utcNow)
            {
                UtcNow = utcNow;
            }

            public DateTime UtcNow { get; set; }
        }

        private sealed class FakeResourceWallet : IResourceWallet
        {
            public List<ResourceGrantRequest> Requests { get; } = new();
            public bool ShouldSucceed { get; set; } = true;

            public bool TryGrant(ResourceGrantRequest request)
            {
                Requests.Add(request);
                return ShouldSucceed;
            }
        }

        private sealed class InMemoryDatasaveService : IDatasaveService
        {
            private readonly Dictionary<string, SaveData> data = new();

            public int SaveCount { get; private set; }

            public T Load<T>(string key = null) where T : SaveData, new()
            {
                if (data.TryGetValue(key, out SaveData saved))
                {
                    return (T)saved;
                }

                T created = new();
                data[key] = created;
                return created;
            }

            public void Save<T>(T saved, string key = null) where T : SaveData
            {
                data[key] = saved;
                SaveCount++;
            }

            public void SaveAll()
            {
            }

            public bool Exists(string key) => data.ContainsKey(key);

            public void Delete(string key) => data.Remove(key);

            public void DeleteAll() => data.Clear();
        }
    }
}
