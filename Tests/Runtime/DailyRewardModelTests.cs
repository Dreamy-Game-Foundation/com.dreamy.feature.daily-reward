using System;
using System.Collections.Generic;
using Dreamy.Datasave;
using Dreamy.DataConfig;
using Dreamy.Economy;
using Dreamy.Economy.Rewards;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Dreamy.DailyReward.Tests
{
    public sealed class DailyRewardModelTests
    {
        [Test]
        public void ResolvedRecovery_UsesJournalWhenDuplicatePendingPayloadIsInvalid()
        {
            var clock = new FakeClock(new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc));
            var save = new InMemoryDatasaveService();
            var currency = new DatasaveResourceWallet(save, "currency");
            var item = new FakeItemDestination(save) { Unavailable = true };
            var model = new DailyRewardModel(BundleSchedule(), save, Gateway(currency, item), clock, "strict");
            Assert.That(model.Claim().Status, Is.EqualTo(DailyRewardClaimStatus.Pending));

            DailyRewardSaveData persisted = save.Load<DailyRewardSaveData>("strict");
            Assert.That(persisted.Journal.State, Is.EqualTo(RewardJournalState.Delivering));
            persisted.PendingClaim.Entries.Clear();
            persisted.PendingClaim.BundleSchemaVersion = 0;
            save.Save(persisted, "strict");
            save = save.Recreate();
            currency = new DatasaveResourceWallet(save, "currency");
            item = new FakeItemDestination(save);
            model = new DailyRewardModel(BundleSchedule(), save, Gateway(currency, item), clock, "strict",
                new CallbackResolver { Callback = (_, __) => throw new AssertionException("Resolved recovery must not resolve again.") });

            DailyRewardClaimResult result = model.Claim();
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Bundle.Entries, Has.Count.EqualTo(2));
            Assert.That(currency.GetBalance(new ResourceId("currency.gold")), Is.EqualTo(100));
            Assert.That(item.Count, Is.EqualTo(1));
            Assert.That(save.Load<DailyRewardSaveData>("strict").Journal.State, Is.EqualTo(RewardJournalState.Completed));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void IntentRecovery_RejectsInvalidOrChangedPayloadBeforeResolverOrGrant(bool validButChanged)
        {
            var clock = new FakeClock(new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc));
            var save = new InMemoryDatasaveService();
            var currency = new DatasaveResourceWallet(save, "currency");
            var item = new FakeItemDestination(save);
            var config = ReadSchedule(BundleJson().Replace("\"schemaVersion\":1", "\"requiresResolution\":true,\"schemaVersion\":1"));
            var model = new DailyRewardModel(config, save, Gateway(currency, item), clock, "strict");
            Assert.That(model.Claim().Reason, Is.EqualTo(RewardDeliveryReason.ResolutionRequired));
            DailyRewardSaveData persisted = save.Load<DailyRewardSaveData>("strict");
            if (validButChanged) persisted.PendingClaim.Entries[0].Amount++;
            else persisted.PendingClaim.Entries.Clear();
            save.Save(persisted, "strict");
            save = save.Recreate();
            currency = new DatasaveResourceWallet(save, "currency");
            item = new FakeItemDestination(save);
            int calls = 0;
            model = new DailyRewardModel(config, save, Gateway(currency, item), clock, "strict",
                new CallbackResolver { Callback = (_, original) => { calls++; return original; } });

            DailyRewardClaimResult result = model.Claim();
            Assert.That(result.Status, Is.EqualTo(DailyRewardClaimStatus.GrantFailed));
            Assert.That(result.Reason, Is.EqualTo(validButChanged
                ? RewardDeliveryReason.PayloadMismatch : RewardDeliveryReason.InvalidJournal));
            Assert.That(calls, Is.Zero);
            Assert.That(currency.GetBalance(new ResourceId("currency.gold")), Is.Zero);
            Assert.That(item.Count, Is.Zero);
            Assert.That(save.Load<DailyRewardSaveData>("strict").Journal.State, Is.EqualTo(RewardJournalState.IntentPersisted));
        }

        [Test]
        public void StopSchedule_AfterFinalClaim_ShowsCompletedAndAllRowsClaimedAfterRecreation()
        {
            var clock = new FakeClock(new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc));
            var save = new InMemoryDatasaveService();
            var config = ReadSchedule(LegacyJson().Replace("Repeat", "StopAfterLastDay"));
            var model = new DailyRewardModel(config, save, new FakeResourceWallet(), clock, "stop");
            Assert.That(model.Claim().IsSuccess, Is.True);
            clock.UtcNow = clock.UtcNow.AddDays(1);
            Assert.That(model.Claim().IsSuccess, Is.True);
            model = new DailyRewardModel(config, save.Recreate(), new FakeResourceWallet(), clock, "stop");

            DailyRewardViewState state = model.GetState();
            Assert.That(state.CanClaim, Is.False);
            Assert.That(state.HasPendingClaim, Is.False);
            Assert.That(state.IsCompleted, Is.True);
            Assert.That(state.Rewards, Has.All.Matches<DailyRewardItemViewState>(r => r.Status == DailyRewardItemStatus.Claimed));
            Assert.That(model.Claim().Status, Is.EqualTo(DailyRewardClaimStatus.Completed));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ResolverRunsAfterIntentAndReplaysOnlyUntilResolvedSaveCommits(bool failResolvedSave)
        {
            var clock = new FakeClock(new DateTime(2026, 10, 14, 23, 59, 0, DateTimeKind.Utc));
            var save = new InMemoryDatasaveService();
            var currency = new DatasaveResourceWallet(save, "currency");
            var item = new FakeItemDestination(save) { Unavailable = true };
            DailyRewardScheduleConfig config = ReadSchedule(BundleJson().Replace("\"schemaVersion\":1", "\"requiresResolution\":true,\"schemaVersion\":1"));
            var resolver = new CallbackResolver();
            DailyRewardModel model = null;
            string requestId = null;
            int callbacks = 0;
            resolver.Callback = (id, original) =>
            {
                Assert.That(save.Load<DailyRewardSaveData>("strict").Journal.State, Is.EqualTo(RewardJournalState.IntentPersisted));
                Assert.That(model.Claim().Status, Is.EqualTo(DailyRewardClaimStatus.Busy));
                Assert.That(currency.GetBalance(new ResourceId("currency.gold")), Is.Zero);
                if (requestId != null) Assert.That(id, Is.EqualTo(requestId));
                requestId = id;
                callbacks++;
                return new ResolvedRewardBundle(1, "fixed-v1", new[]
                {
                    original.Entries[0], new ResolvedRewardEntry("card", "item", "item.card", 3)
                });
            };
            model = new DailyRewardModel(config, save, Gateway(currency, item), clock, "strict", resolver);
            if (failResolvedSave) save.FailDailySaveAttempt = 3;

            Assert.That(model.Claim().Status, Is.EqualTo(DailyRewardClaimStatus.Pending));
            Assert.That(save.Load<DailyRewardSaveData>("strict").Journal.State,
                Is.EqualTo(failResolvedSave ? RewardJournalState.IntentPersisted : RewardJournalState.Delivering));
            if (failResolvedSave)
            {
                Assert.That(currency.GetBalance(new ResourceId("currency.gold")), Is.Zero);
                Assert.That(save.Load<DailyRewardSaveData>("strict").PendingClaim.GetBundle().Entries[1].Amount, Is.EqualTo(1));
            }
            save = save.Recreate();
            currency = new DatasaveResourceWallet(save, "currency");
            item = new FakeItemDestination(save);
            clock.UtcNow = clock.UtcNow.AddMinutes(2);
            // A changed catalog and resolver must not replace a persisted resolved payload.
            DailyRewardScheduleConfig changed = ReadSchedule(BundleJson().Replace("\"amount\":1}", "\"amount\":9}"));
            model = new DailyRewardModel(changed, save, Gateway(currency, item), clock, "strict",
                failResolvedSave ? resolver : new CallbackResolver { Callback = (_, __) => throw new AssertionException("Resolved claim was re-resolved.") });

            Assert.That(model.Claim().IsSuccess, Is.True);
            Assert.That(callbacks, Is.EqualTo(failResolvedSave ? 2 : 1));
            Assert.That(currency.GetBalance(new ResourceId("currency.gold")), Is.EqualTo(100));
            Assert.That(item.Count, Is.EqualTo(3));
            Assert.That(save.Load<DailyRewardSaveData>("strict").LastClaimUtcTicks, Is.EqualTo(clock.UtcNow.Date.AddDays(-1).Ticks));
            Assert.That(save.Load<DailyRewardSaveData>("strict").Journal.RequestId, Is.EqualTo(requestId));
        }

        [Test]
        public void RequiredResolverMissingOrUnavailableKeepsIntentWithoutGrant()
        {
            var clock = new FakeClock(new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc));
            var save = new InMemoryDatasaveService();
            var currency = new DatasaveResourceWallet(save, "currency");
            var item = new FakeItemDestination(save);
            var config = ReadSchedule(BundleJson().Replace("\"schemaVersion\":1", "\"requiresResolution\":true,\"schemaVersion\":1"));
            var model = new DailyRewardModel(config, save, Gateway(currency, item), clock, "strict");
            Assert.That(model.Claim().Reason, Is.EqualTo(RewardDeliveryReason.ResolutionRequired));
            var resolver = new CallbackResolver { Callback = (_, __) => throw new InvalidOperationException("Offline") };
            model = new DailyRewardModel(config, save, Gateway(currency, item), clock, "strict", resolver);
            Assert.That(model.Claim().Status, Is.EqualTo(DailyRewardClaimStatus.Pending));
            Assert.That(currency.GetBalance(new ResourceId("currency.gold")), Is.Zero);
            Assert.That(item.Count, Is.Zero);
            Assert.That(save.Load<DailyRewardSaveData>("strict").Journal.State, Is.EqualTo(RewardJournalState.IntentPersisted));
            Assert.That(save.Load<DailyRewardSaveData>("strict").LastClaimUtcTicks, Is.Zero);
        }

        private sealed class CallbackResolver : IDailyRewardBundleResolver
        {
            public Func<string, ResolvedRewardBundle, ResolvedRewardBundle> Callback;
            public RewardDeliveryResult Resolve(string requestId, ResolvedRewardBundle original, out ResolvedRewardBundle resolved)
            {
                resolved = Callback(requestId, original);
                return RewardDeliveryResult.Delivered();
            }
        }

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

        [Test]
        public void Claim_WhenLegacySaveFails_DoesNotMutateLoadedSaveOrAdvanceProgress()
        {
            var clock = new FakeClock(new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc));
            var wallet = new FakeResourceWallet();
            DailyRewardModel model = CreateModel(clock, wallet, out InMemoryDatasaveService save);
            DailyRewardSaveData loaded = save.Load<DailyRewardSaveData>("test-save");
            save.FailDailySaveAttempt = 2;

            DailyRewardClaimResult result = model.Claim();

            Assert.That(result.Status, Is.EqualTo(DailyRewardClaimStatus.Pending));
            Assert.That(result.Reason, Is.EqualTo(RewardDeliveryReason.PersistenceFailed));
            Assert.That(loaded.NextRewardIndex, Is.Zero);
            Assert.That(loaded.LastClaimUtcTicks, Is.Zero);
            Assert.That(model.GetState().CanClaim, Is.True);
            Assert.That(wallet.Requests[0].TransactionId, Is.EqualTo("test-schedule:0:day-1"));
            Assert.That(save.SaveCount, Is.EqualTo(1));
        }

        [TestCase(2)] // intent
        [TestCase(3)] // resolved payload
        [TestCase(4)] // currency acknowledgement, after owner commit
        [TestCase(5)] // item acknowledgement, after owner commit
        [TestCase(6)] // claimed + Completed journal
        public void Claim_WhenStrictPersistenceFails_RecreatedOwnersResumeOriginalClaim(int failedSave)
        {
            var clock = new FakeClock(new DateTime(2026, 10, 14, 23, 59, 0, DateTimeKind.Utc));
            DailyRewardScheduleConfig config = BundleSchedule();
            var save = new InMemoryDatasaveService();
            var currency = new DatasaveResourceWallet(save, "currency");
            var item = new FakeItemDestination(save);
            var model = new DailyRewardModel(config, save, Gateway(currency, item), clock, "strict");
            save.FailDailySaveAttempt = failedSave;

            DailyRewardClaimResult failed = model.Claim();
            DailyRewardSaveData beforeRestart = save.Load<DailyRewardSaveData>("strict");

            Assert.That(failed.Status, Is.EqualTo(DailyRewardClaimStatus.Pending));
            Assert.That(failed.Reason, Is.EqualTo(RewardDeliveryReason.PersistenceFailed));
            Assert.That(beforeRestart.NextRewardIndex, Is.Zero);
            Assert.That(beforeRestart.Cycle, Is.Zero);
            Assert.That(beforeRestart.LastClaimUtcTicks, Is.Zero);
            Assert.That(model.GetState().CanClaim, Is.True);
            bool hasIntent = failedSave != 2;
            Assert.That(model.GetState().HasPendingClaim, Is.EqualTo(hasIntent));

            // Restart from serialized persisted documents, not the live owner objects.
            save = save.Recreate();
            currency = new DatasaveResourceWallet(save, "currency");
            item = new FakeItemDestination(save);
            clock.UtcNow = clock.UtcNow.AddMinutes(2);
            model = new DailyRewardModel(config, save, Gateway(currency, item), clock, "strict");

            DailyRewardClaimResult recovered = model.Claim();
            DailyRewardSaveData committed = save.Load<DailyRewardSaveData>("strict");

            Assert.That(recovered.Status, Is.EqualTo(DailyRewardClaimStatus.Claimed));
            Assert.That(recovered.Bundle.Entries, Has.Count.EqualTo(2));
            Assert.That(recovered.Reward.HasValue, Is.False);
            Assert.That(currency.GetBalance(new ResourceId("currency.gold")), Is.EqualTo(100));
            Assert.That(item.Count, Is.EqualTo(1));
            Assert.That(committed.NextRewardIndex, Is.EqualTo(1));
            Assert.That(committed.Cycle, Is.Zero);
            Assert.That(committed.Journal.State, Is.EqualTo(RewardJournalState.Completed));
            Assert.That(committed.LastClaimUtcTicks, Is.EqualTo(
                new DateTime(2026, 10, hasIntent ? 14 : 15, 0, 0, 0, DateTimeKind.Utc).Ticks));
            Assert.That(committed.PendingClaim.ClaimCycle, Is.Zero);
            Assert.That(committed.PendingClaim.NextRewardIndex, Is.EqualTo(1));
            // A persisted intent retains its original day; an unpersisted intent never
            // dispatched an owner and therefore starts a new claim on today's date.
            Assert.That(model.GetState().CanClaim, Is.EqualTo(hasIntent));
        }

        [Test]
        public void Claim_WhenItemOwnerIsUnavailable_ResumesAcrossMidnightWithoutDuplicateCurrency()
        {
            var clock = new FakeClock(new DateTime(2026, 10, 14, 23, 59, 0, DateTimeKind.Utc));
            var save = new InMemoryDatasaveService();
            var currency = new DatasaveResourceWallet(save, "currency");
            var item = new FakeItemDestination(save) { Unavailable = true };
            DailyRewardScheduleConfig config = BundleSchedule();
            var model = new DailyRewardModel(config, save, Gateway(currency, item), clock, "strict");

            DailyRewardClaimResult failed = model.Claim();

            Assert.That(failed.Status, Is.EqualTo(DailyRewardClaimStatus.Pending));
            Assert.That(failed.Reason, Is.EqualTo(RewardDeliveryReason.DestinationUnavailable));
            Assert.That(currency.GetBalance(new ResourceId("currency.gold")), Is.EqualTo(100));
            Assert.That(item.Count, Is.Zero);
            Assert.That(model.GetState().HasPendingClaim, Is.True);

            save = save.Recreate();
            currency = new DatasaveResourceWallet(save, "currency");
            item = new FakeItemDestination(save);
            clock.UtcNow = clock.UtcNow.AddMinutes(2);
            model = new DailyRewardModel(config, save, Gateway(currency, item), clock, "strict");

            Assert.That(model.Claim().IsSuccess, Is.True);
            Assert.That(save.Load<DailyRewardSaveData>("strict").LastClaimUtcTicks,
                Is.EqualTo(new DateTime(2026, 10, 14, 0, 0, 0, DateTimeKind.Utc).Ticks));
            Assert.That(currency.GetBalance(new ResourceId("currency.gold")), Is.EqualTo(100));
            Assert.That(item.Count, Is.EqualTo(1));
            Assert.That(model.GetState().Rewards[1].Status, Is.EqualTo(DailyRewardItemStatus.Claimable));
            // Claiming the new day is a separate user action after old-day recovery.
            Assert.That(model.Claim().IsSuccess, Is.True);
            Assert.That(currency.GetBalance(new ResourceId("currency.gems")), Is.EqualTo(5));
            Assert.That(model.Claim().Status, Is.EqualTo(DailyRewardClaimStatus.NotAvailable));
        }

        [Test]
        public void Claim_WhenStrictRepeatFinishes_UsesNewCycleIdentityWithoutSameDayReplay()
        {
            var clock = new FakeClock(new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc));
            var save = new InMemoryDatasaveService();
            var currency = new DatasaveResourceWallet(save, "currency");
            var item = new FakeItemDestination(save);
            var model = new DailyRewardModel(BundleSchedule(), save, Gateway(currency, item), clock, "strict");
            Assert.That(model.Claim().IsSuccess, Is.True);
            string firstId = save.Load<DailyRewardSaveData>("strict").Journal.RequestId;
            clock.UtcNow = clock.UtcNow.AddDays(1);

            DailyRewardClaimResult finalDay = model.Claim();

            Assert.That(finalDay.Reward.Value.Amount, Is.EqualTo(5));
            Assert.That(save.Load<DailyRewardSaveData>("strict").Cycle, Is.EqualTo(1));
            Assert.That(save.Load<DailyRewardSaveData>("strict").NextRewardIndex, Is.Zero);
            Assert.That(model.Claim().Status, Is.EqualTo(DailyRewardClaimStatus.NotAvailable));
            clock.UtcNow = clock.UtcNow.AddDays(1);
            Assert.That(model.Claim().IsSuccess, Is.True);
            Assert.That(save.Load<DailyRewardSaveData>("strict").Journal.RequestId,
                Is.EqualTo(RewardIdentity.Request("strict", "daily", "bundle-schedule", "1", "day-1")));
            Assert.That(save.Load<DailyRewardSaveData>("strict").Journal.RequestId, Is.Not.EqualTo(firstId));
            Assert.That(currency.GetBalance(new ResourceId("currency.gold")), Is.EqualTo(200));
            Assert.That(item.Count, Is.EqualTo(2));
        }

        [Test]
        public void Claim_WhenCallbacksReenter_ReturnsBusyWithoutSecondClaim()
        {
            var clock = new FakeClock(new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc));
            var save = new InMemoryDatasaveService();
            var currency = new DatasaveResourceWallet(save, "currency");
            var item = new FakeItemDestination(save);
            var model = new DailyRewardModel(BundleSchedule(), save, Gateway(currency, item), clock, "strict");
            int callbacks = 0;
            save.BeforeSave = _ =>
            {
                callbacks++;
                Assert.That(model.Claim().Status, Is.EqualTo(DailyRewardClaimStatus.Busy));
                Assert.That(model.GetState().IsBusy, Is.True);
                Assert.That(model.GetState().CanClaim, Is.False);
            };

            Assert.That(model.Claim().IsSuccess, Is.True);
            Assert.That(callbacks, Is.GreaterThan(0));
            Assert.That(item.Count, Is.EqualTo(1));
            Assert.That(currency.GetBalance(new ResourceId("currency.gold")), Is.EqualTo(100));
        }

        [Test]
        public void Claim_WhenLegacyWalletReenters_ReturnsBusyAndKeepsLegacySaveCount()
        {
            var clock = new FakeClock(new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc));
            var wallet = new FakeResourceWallet();
            DailyRewardModel model = CreateModel(clock, wallet, out InMemoryDatasaveService save);
            DailyRewardClaimResult nested = default;
            wallet.OnGrant = () => nested = model.Claim();

            Assert.That(model.Claim().IsSuccess, Is.True);
            Assert.That(nested.Status, Is.EqualTo(DailyRewardClaimStatus.Busy));
            Assert.That(wallet.Requests, Has.Count.EqualTo(1));
            Assert.That(save.SaveCount, Is.EqualTo(2));
        }

        [TestCase("\"resourceId\":\"currency.gold\",\"amount\":100,\"bundle\":{\"schemaVersion\":1,\"resolverVersion\":\"v1\",\"entries\":[{\"entryId\":\"a\",\"kindId\":\"currency\",\"targetId\":\"currency.gold\",\"amount\":1}]}")]
        [TestCase("\"bundle\":{\"schemaVersion\":2,\"resolverVersion\":\"v1\",\"entries\":[{\"entryId\":\"a\",\"kindId\":\"currency\",\"targetId\":\"currency.gold\",\"amount\":1}]}")]
        [TestCase("\"bundle\":{\"schemaVersion\":1,\"resolverVersion\":\"v1\",\"entries\":[{\"entryId\":\"a\",\"kindId\":\"item\",\"targetId\":\"item.card\",\"amount\":1},{\"entryId\":\"a\",\"kindId\":\"item\",\"targetId\":\"item.card\",\"amount\":2}]}")]
        public void Initialize_WhenRewardConfigIsAmbiguousOrInvalid_RejectsIt(string rewardJson)
        {
            DailyRewardScheduleConfig config = JsonConvert.DeserializeObject<DailyRewardScheduleConfig>(
                "{\"scheduleId\":\"test\",\"cycleMode\":\"Repeat\",\"rewards\":[{\"id\":\"day-1\",\"day\":1," + rewardJson + "}]}");
            Assert.Throws<DataConfigException>(() => config.Initialize("test"));
        }

        [Test]
        public void Initialize_WhenScheduleSchemaIsUnknown_RejectsIt()
        {
            DailyRewardScheduleConfig config = JsonConvert.DeserializeObject<DailyRewardScheduleConfig>(
                LegacyJson().Replace("\"scheduleId\"", "\"schemaVersion\":99,\"scheduleId\""));
            Assert.Throws<DataConfigException>(() => config.Initialize("test"));
        }

        [Test]
        public void Claim_WhenBundleContainsUnknownOwner_GrantsNothing()
        {
            var clock = new FakeClock(new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc));
            var save = new InMemoryDatasaveService();
            var currency = new DatasaveResourceWallet(save, "currency");
            var gateway = new RewardDeliveryGateway(new IRewardDestination[] { currency });
            var model = new DailyRewardModel(BundleSchedule(), save, gateway, clock, "strict");

            DailyRewardClaimResult result = model.Claim();

            Assert.That(result.Status, Is.EqualTo(DailyRewardClaimStatus.GrantFailed));
            Assert.That(result.Reason, Is.EqualTo(RewardDeliveryReason.UnknownKind));
            Assert.That(result.Retryable, Is.False);
            Assert.That(currency.GetBalance(new ResourceId("currency.gold")), Is.Zero);
            Assert.That(save.Load<DailyRewardSaveData>("strict").NextRewardIndex, Is.Zero);
        }

        [Test]
        public void Constructor_WhenBundleUsesLegacyWallet_RejectsItWithoutSaving()
        {
            var save = new InMemoryDatasaveService();
            Assert.Throws<ArgumentException>(() => new DailyRewardModel(BundleSchedule(), save,
                new FakeResourceWallet(), new FakeClock(new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc)), "test"));
            Assert.That(save.SaveCount, Is.Zero);
        }

        [Test]
        public void Constructor_WhenVersionOneSaveLoads_PreservesCountersAndRoundTripsLegacyConfig()
        {
            var save = new InMemoryDatasaveService();
            DailyRewardSaveData legacy = JsonConvert.DeserializeObject<DailyRewardSaveData>(
                "{\"scheduleId\":\"test-schedule\",\"nextRewardIndex\":1,\"cycle\":8,\"lastClaimUtcTicks\":639275040000000000}");
            save.Save(legacy, "test");
            DailyRewardScheduleConfig config = ReadSchedule(LegacyJson());
            config = ReadSchedule(JsonConvert.SerializeObject(config));
            var model = new DailyRewardModel(config, save, new FakeResourceWallet(),
                new FakeClock(new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc)), "test");

            DailyRewardSaveData migrated = save.Recreate().Load<DailyRewardSaveData>("test");
            Assert.That(migrated.Version, Is.EqualTo(2));
            Assert.That(migrated.NextRewardIndex, Is.EqualTo(1));
            Assert.That(migrated.Cycle, Is.EqualTo(8));
            Assert.That(migrated.LastClaimUtcTicks, Is.EqualTo(legacy.LastClaimUtcTicks));
            Assert.That(migrated.ActiveSchedule.ScheduleId, Is.EqualTo("test-schedule"));
        }

        [Test]
        public void Claim_WhenRevisionChangesMidCycle_DefersRewardsUntilNextCycleWithoutReset()
        {
            var clock = new FakeClock(new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc));
            var wallet = new FakeResourceWallet();
            DailyRewardModel model = CreateModel(clock, wallet, out InMemoryDatasaveService save);
            Assert.That(model.Claim().IsSuccess, Is.True);
            DailyRewardScheduleConfig changed = ReadSchedule(LegacyJson().Replace("\"amount\":5", "\"amount\":50")
                .Replace("\"scheduleId\"", "\"revision\":\"2\",\"scheduleId\""));
            model = new DailyRewardModel(changed, save, wallet, clock, "test-save");

            Assert.That(model.GetState().Rewards[1].Entry.Reward.Amount, Is.EqualTo(5));
            Assert.That(model.GetState().CanClaim, Is.False);
            clock.UtcNow = clock.UtcNow.AddDays(1);
            Assert.That(model.Claim().Reward.Value.Amount, Is.EqualTo(5));
            clock.UtcNow = clock.UtcNow.AddDays(1);
            Assert.That(model.GetState().Rewards[1].Entry.Reward.Amount, Is.EqualTo(50));
            Assert.That(model.Claim().IsSuccess, Is.True);
            Assert.That(wallet.Requests[2].TransactionId, Is.EqualTo("test-schedule:1:day-1"));
            Assert.That(save.Load<DailyRewardSaveData>("test-save").ActiveSchedule.Revision, Is.EqualTo("2"));
        }

        [Test]
        public void Claim_WhenScheduleIdentityChanges_RequiresMigrationWithoutResettingSave()
        {
            var clock = new FakeClock(new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc));
            var wallet = new FakeResourceWallet();
            DailyRewardModel model = CreateModel(clock, wallet, out InMemoryDatasaveService save);
            model.Claim();
            int saves = save.SaveCount;
            model = new DailyRewardModel(ReadSchedule(LegacyJson().Replace("test-schedule", "new-schedule")),
                save, wallet, clock, "test-save");

            Assert.That(model.GetState().MigrationRequired, Is.True);
            Assert.That(model.GetState().CanClaim, Is.False);
            Assert.That(model.Claim().Status, Is.EqualTo(DailyRewardClaimStatus.MigrationRequired));
            Assert.That(save.Load<DailyRewardSaveData>("test-save").NextRewardIndex, Is.EqualTo(1));
            Assert.That(save.SaveCount, Is.EqualTo(saves));
            Assert.That(wallet.Requests, Has.Count.EqualTo(1));
        }

        [Test]
        public void Claim_WhenPendingScheduleChanges_RecoversSavedPayloadBeforeMigrationCheck()
        {
            var clock = new FakeClock(new DateTime(2026, 10, 14, 23, 59, 0, DateTimeKind.Utc));
            var save = new InMemoryDatasaveService();
            var currency = new DatasaveResourceWallet(save, "currency");
            var item = new FakeItemDestination(save) { Unavailable = true };
            var model = new DailyRewardModel(BundleSchedule(), save, Gateway(currency, item), clock, "strict");
            Assert.That(model.Claim().Status, Is.EqualTo(DailyRewardClaimStatus.Pending));
            string requestId = save.Load<DailyRewardSaveData>("strict").Journal.RequestId;
            save = save.Recreate();
            currency = new DatasaveResourceWallet(save, "currency");
            item = new FakeItemDestination(save);
            DailyRewardScheduleConfig changed = ReadSchedule(BundleJson().Replace("bundle-schedule", "new-identity")
                .Replace("\"amount\":1}", "\"amount\":9}"));
            clock.UtcNow = clock.UtcNow.AddMinutes(2);
            model = new DailyRewardModel(changed, save, Gateway(currency, item), clock, "strict");

            Assert.That(model.Claim().IsSuccess, Is.True);
            Assert.That(item.Count, Is.EqualTo(1));
            Assert.That(save.Load<DailyRewardSaveData>("strict").Journal.RequestId, Is.EqualTo(requestId));
            Assert.That(save.Load<DailyRewardSaveData>("strict").PendingClaim.Revision, Is.EqualTo("1"));
            Assert.That(model.Claim().Status, Is.EqualTo(DailyRewardClaimStatus.MigrationRequired));
        }

        [TestCase(true, 100, "test-schedule:1:day-1")]
        [TestCase(false, 5, "test-schedule:0:day-2")]
        public void Claim_WhenDaysAreSkipped_UsesConfiguredResetPolicy(bool reset, long amount, string id)
        {
            var clock = new FakeClock(new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc));
            var wallet = new FakeResourceWallet();
            DailyRewardScheduleConfig config = ReadSchedule(LegacyJson()
                .Replace("\"resetProgressAfterMissedDay\":true,", reset ? "" : "\"resetProgressAfterMissedDay\":false,"));
            var model = new DailyRewardModel(config, new InMemoryDatasaveService(), wallet, clock, "test");
            Assert.That(config.ResetProgressAfterMissedDay, Is.EqualTo(reset));
            model.Claim();
            clock.UtcNow = clock.UtcNow.AddDays(3);

            Assert.That(model.Claim().Reward.Value.Amount, Is.EqualTo(amount));
            Assert.That(wallet.Requests[1].TransactionId, Is.EqualTo(id));
        }

        [Test]
        public void Claim_WhenClockRollsBack_DoesNotReopenClaimedDate()
        {
            var clock = new FakeClock(new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc));
            var wallet = new FakeResourceWallet();
            DailyRewardModel model = CreateModel(clock, wallet, out _);
            model.Claim();
            clock.UtcNow = clock.UtcNow.AddDays(-1);

            Assert.That(model.Claim().Status, Is.EqualTo(DailyRewardClaimStatus.NotAvailable));
            Assert.That(wallet.Requests, Has.Count.EqualTo(1));
            Assert.That(model.GetState().NextClaimUtc, Is.EqualTo(new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc)));
        }

        [Test]
        public void Claim_WhenStopScheduleIsCompleted_StaysCompletedAfterSkippedDaysAndRevision()
        {
            var clock = new FakeClock(new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc));
            var wallet = new FakeResourceWallet();
            var save = new InMemoryDatasaveService();
            string json = LegacyJson().Replace("Repeat", "StopAfterLastDay");
            var model = new DailyRewardModel(ReadSchedule(json), save, wallet, clock, "test");
            model.Claim();
            clock.UtcNow = clock.UtcNow.AddDays(1);
            model.Claim();
            clock.UtcNow = clock.UtcNow.AddDays(3);
            model = new DailyRewardModel(ReadSchedule(json.Replace("\"amount\":5", "\"amount\":50")), save, wallet, clock, "test");

            Assert.That(model.Claim().Status, Is.EqualTo(DailyRewardClaimStatus.Completed));
            Assert.That(wallet.Requests, Has.Count.EqualTo(2));
            Assert.That(save.Load<DailyRewardSaveData>("test").NextRewardIndex, Is.EqualTo(2));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void GetState_WhenRevisionRemovesOrReordersEntries_KeepsCurrentCycleSnapshot(bool remove)
        {
            var clock = new FakeClock(new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc));
            var wallet = new FakeResourceWallet();
            DailyRewardModel model = CreateModel(clock, wallet, out InMemoryDatasaveService save);
            model.Claim();
            string changedJson = remove
                ? "{\"scheduleId\":\"test-schedule\",\"cycleMode\":\"Repeat\",\"rewards\":[{\"id\":\"day-1\",\"day\":1,\"resourceId\":\"currency.gold\",\"amount\":100}]}"
                : LegacyJson().Replace("\"id\":\"day-1\"", "\"id\":\"swap\"")
                    .Replace("\"id\":\"day-2\"", "\"id\":\"day-1\"").Replace("\"id\":\"swap\"", "\"id\":\"day-2\"");
            model = new DailyRewardModel(ReadSchedule(changedJson), save.Recreate(), wallet, clock, "test-save");

            Assert.That(model.GetState().Rewards, Has.Count.EqualTo(2));
            Assert.That(model.GetState().Rewards[0].Entry.Id, Is.EqualTo("day-1"));
            Assert.That(model.GetState().Rewards[0].Status, Is.EqualTo(DailyRewardItemStatus.Claimed));
            Assert.That(model.GetState().Rewards[1].Entry.Id, Is.EqualTo("day-2"));
            clock.UtcNow = clock.UtcNow.AddDays(1);
            Assert.That(model.Claim().Reward.Value.Amount, Is.EqualTo(5));
            Assert.That(wallet.Requests[1].TransactionId, Is.EqualTo("test-schedule:0:day-2"));
            Assert.That(model.GetState().Rewards.Count, Is.EqualTo(remove ? 1 : 2));
        }

        [TestCase("currency")]
        [TestCase("items")]
        public void Claim_WhenOwnerSaveFails_RecreationDoesNotLoseOrDuplicateEffects(string owner)
        {
            var clock = new FakeClock(new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc));
            var save = new InMemoryDatasaveService();
            var currency = new DatasaveResourceWallet(save, "currency");
            var item = new FakeItemDestination(save);
            var model = new DailyRewardModel(BundleSchedule(), save, Gateway(currency, item), clock, "strict");
            save.FailSaveKey = owner;

            Assert.That(model.Claim().Status, Is.EqualTo(DailyRewardClaimStatus.Pending));
            Assert.That(currency.GetBalance(new ResourceId("currency.gold")), Is.EqualTo(owner == "currency" ? 0 : 100));
            Assert.That(item.Count, Is.Zero);
            Assert.That(save.Load<DailyRewardSaveData>("strict").NextRewardIndex, Is.Zero);

            save = save.Recreate();
            currency = new DatasaveResourceWallet(save, "currency");
            item = new FakeItemDestination(save);
            model = new DailyRewardModel(BundleSchedule(), save, Gateway(currency, item), clock, "strict");

            Assert.That(model.Claim().IsSuccess, Is.True);
            Assert.That(currency.GetBalance(new ResourceId("currency.gold")), Is.EqualTo(100));
            Assert.That(item.Count, Is.EqualTo(1));
            Assert.That(model.Claim().Status, Is.EqualTo(DailyRewardClaimStatus.NotAvailable));
        }

        [Test]
        public void Presenter_WhenBundleSucceeds_SupportsLegacyFeedbackAndRebindsWithoutDuplicateListeners()
        {
            var clock = new FakeClock(new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc));
            var save = new InMemoryDatasaveService();
            var currency = new DatasaveResourceWallet(save, "currency");
            var item = new FakeItemDestination(save);
            var model = new DailyRewardModel(BundleSchedule(), save, Gateway(currency, item), clock, "strict");
            var view = new FakeView();
            var legacyFeedback = new FakeFeedback();
            var presenter = new DailyRewardPresenter(model, view, legacyFeedback);

            presenter.Show();
            presenter.Show();
            Assert.That(view.ClaimListenerCount, Is.EqualTo(1));
            view.RequestClaim();
            Assert.That(view.Result.IsSuccess, Is.True);
            Assert.That(view.Result.Bundle.Entries, Has.Count.EqualTo(2));
            Assert.That(legacyFeedback.ClaimedCount, Is.Zero);
            Assert.That(view.Interactable, Is.False);
            view.RequestClose();
            Assert.That(view.ClaimListenerCount, Is.Zero);
            Assert.That(view.CloseCount, Is.EqualTo(1));
            presenter.Show();
            Assert.That(view.ClaimListenerCount, Is.EqualTo(1));
            presenter.Dispose();
            view.RequestClaim();
            Assert.That(item.Count, Is.EqualTo(1));
        }

        [Test]
        public void Presenter_WhenPendingThenRetried_PublishesReasonAndPlaysBundleFeedbackOnce()
        {
            var clock = new FakeClock(new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc));
            var save = new InMemoryDatasaveService();
            var currency = new DatasaveResourceWallet(save, "currency");
            var item = new FakeItemDestination(save) { Unavailable = true };
            var model = new DailyRewardModel(BundleSchedule(), save, Gateway(currency, item), clock, "strict");
            var view = new FakeView();
            var feedback = new FakeBundleFeedback();
            var presenter = new DailyRewardPresenter(model, view, feedback);
            presenter.Show();

            view.RequestClaim();

            Assert.That(view.Result.Status, Is.EqualTo(DailyRewardClaimStatus.Pending));
            Assert.That(view.Result.Reason, Is.EqualTo(RewardDeliveryReason.DestinationUnavailable));
            Assert.That(view.Interactable, Is.True);
            Assert.That(view.State.HasPendingClaim, Is.True);
            Assert.That(feedback.BundleCount, Is.Zero);
            item.Unavailable = false;

            view.RequestClaim();

            Assert.That(view.Result.IsSuccess, Is.True);
            Assert.That(feedback.BundleCount, Is.EqualTo(1));
            Assert.That(feedback.ClaimedCount, Is.Zero);
            Assert.That(view.Interactable, Is.False);
            Assert.That(view.State.HasPendingClaim, Is.False);
            presenter.Dispose();
        }

        [Test]
        public void Presenter_WhenLegacyClaimSucceeds_PlaysLegacyFeedback()
        {
            var clock = new FakeClock(new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc));
            DailyRewardModel model = CreateModel(clock, new FakeResourceWallet(), out _);
            var view = new FakeView();
            var feedback = new FakeFeedback();
            var presenter = new DailyRewardPresenter(model, view, feedback);
            presenter.Show();

            view.RequestClaim();

            Assert.That(feedback.ClaimedCount, Is.EqualTo(1));
            Assert.That(view.Result.Reward.Value.Amount, Is.EqualTo(100));
            presenter.Dispose();
        }

        private static DailyRewardScheduleConfig ReadSchedule(string json)
        {
            DailyRewardScheduleConfig config = JsonConvert.DeserializeObject<DailyRewardScheduleConfig>(json);
            config.Initialize("test");
            return config;
        }

        private static string LegacyJson() =>
            "{\"scheduleId\":\"test-schedule\",\"cycleMode\":\"Repeat\",\"resetProgressAfterMissedDay\":true,\"rewards\":[{\"id\":\"day-1\",\"day\":1,\"resourceId\":\"currency.gold\",\"amount\":100},{\"id\":\"day-2\",\"day\":2,\"resourceId\":\"currency.gems\",\"amount\":5}]}";

        private static string BundleJson() =>
            "{\"scheduleId\":\"bundle-schedule\",\"cycleMode\":\"Repeat\",\"rewards\":[{\"id\":\"day-1\",\"day\":1,\"bundle\":{\"schemaVersion\":1,\"resolverVersion\":\"v1\",\"entries\":[{\"entryId\":\"gold\",\"kindId\":\"currency\",\"targetId\":\"currency.gold\",\"amount\":100},{\"entryId\":\"card\",\"kindId\":\"item\",\"targetId\":\"item.card\",\"amount\":1}]}},{\"id\":\"day-2\",\"day\":2,\"resourceId\":\"currency.gems\",\"amount\":5}]}";

        private static DailyRewardScheduleConfig BundleSchedule() => ReadSchedule(BundleJson());

        private static RewardDeliveryGateway Gateway(DatasaveResourceWallet currency, FakeItemDestination item) =>
            new(new IRewardDestination[] { currency, item });

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
            public Action OnGrant { get; set; }

            public bool TryExchange(ResourceExchangeRequest request) => false;

            public bool TryGrant(ResourceGrantRequest request)
            {
                Requests.Add(request);
                OnGrant?.Invoke();
                return ShouldSucceed;
            }
        }

        private sealed class InMemoryDatasaveService : IDatasaveService
        {
            private readonly Dictionary<string, SaveData> data = new();

            public int SaveCount { get; private set; }
            public int DailySaveAttempts { get; private set; }
            public int FailDailySaveAttempt { get; set; }
            public Action<SaveData> BeforeSave { get; set; }
            public string FailSaveKey { get; set; }

            public InMemoryDatasaveService Recreate()
            {
                var recreated = new InMemoryDatasaveService();
                foreach (KeyValuePair<string, SaveData> pair in data)
                    recreated.data.Add(pair.Key, (SaveData)JsonConvert.DeserializeObject(
                        JsonConvert.SerializeObject(pair.Value), pair.Value.GetType()));
                return recreated;
            }

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
                if (saved is DailyRewardSaveData && ++DailySaveAttempts == FailDailySaveAttempt)
                    throw new InvalidOperationException("Injected Daily persistence failure.");
                if (key == FailSaveKey) throw new InvalidOperationException("Injected owner persistence failure.");
                BeforeSave?.Invoke(saved);
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

        private sealed class FakeItemDestination : IRewardDestination
        {
            private readonly InMemoryDatasaveService save;
            private FakeItemSaveData data;

            public FakeItemDestination(InMemoryDatasaveService save)
            {
                this.save = save;
                data = save.Load<FakeItemSaveData>("items");
            }

            public string KindId => "item";
            public long Count => data.Count;
            public bool Unavailable { get; set; }

            public RewardDeliveryResult Validate(IReadOnlyList<RewardDeliveryRequest> requests)
            {
                foreach (RewardDeliveryRequest request in requests)
                {
                    if (request.Entry.TargetId != "item.card") return RewardDeliveryResult.Failed(RewardDeliveryReason.UnknownTarget);
                    if (data.Receipts.TryGetValue(request.TransactionId, out string receipt) && receipt != request.Fingerprint)
                        return RewardDeliveryResult.Failed(RewardDeliveryReason.PayloadMismatch);
                }
                return RewardDeliveryResult.Delivered();
            }

            public RewardDeliveryResult Deliver(RewardDeliveryRequest request)
            {
                if (Unavailable) return RewardDeliveryResult.Pending(RewardDeliveryReason.DestinationUnavailable);
                RewardDeliveryResult valid = Validate(new[] { request });
                if (!valid.IsSuccess) return valid;
                if (data.Receipts.ContainsKey(request.TransactionId)) return RewardDeliveryResult.AlreadyDelivered();
                var candidate = new FakeItemSaveData
                {
                    Count = checked(data.Count + request.Entry.Amount),
                    Receipts = new Dictionary<string, string>(data.Receipts)
                };
                candidate.Receipts.Add(request.TransactionId, request.Fingerprint);
                save.Save(candidate, "items");
                data = candidate;
                return RewardDeliveryResult.Delivered();
            }
        }

        public sealed class FakeItemSaveData : SaveData
        {
            public long Count { get; set; }
            public Dictionary<string, string> Receipts { get; set; } = new();
        }

        private sealed class FakeView : IDailyRewardView
        {
            public event Action ClaimRequested;
            public event Action CloseRequested;
            public int ClaimListenerCount => ClaimRequested?.GetInvocationList().Length ?? 0;
            public DailyRewardViewState State { get; private set; }
            public DailyRewardClaimResult Result { get; private set; }
            public bool Interactable { get; private set; }
            public int CloseCount { get; private set; }
            public void Render(DailyRewardViewState state) => State = state;
            public void SetClaimInteractable(bool interactable) => Interactable = interactable;
            public void ShowClaimResult(DailyRewardClaimResult result) => Result = result;
            public void Close() => CloseCount++;
            public void RequestClaim() => ClaimRequested?.Invoke();
            public void RequestClose() => CloseRequested?.Invoke();
        }

        private sealed class FakeFeedback : IDailyRewardFeedback
        {
            public int ClaimedCount { get; private set; }
            public void PlayClaimed(ResourceAmount reward) => ClaimedCount++;
            public void PlayClaimFailed() { }
        }

        private sealed class FakeBundleFeedback : IDailyRewardFeedback, IDailyRewardBundleFeedback
        {
            public int ClaimedCount { get; private set; }
            public int BundleCount { get; private set; }
            public void PlayClaimed(ResourceAmount reward) => ClaimedCount++;
            public void PlayClaimed(ResolvedRewardBundle bundle) => BundleCount++;
            public void PlayClaimFailed() { }
        }
    }
}
