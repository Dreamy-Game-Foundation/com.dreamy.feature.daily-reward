# Dreamy Daily Reward

`com.dreamy.feature.daily-reward` is an MVP feature package. It owns schedule evaluation and saved claim progress; the host owns its UI, economy transaction, audio, art, and localization.

## Installation

Add the package and dependencies to the host manifest. During bootstrap, register the schedule before `IDataConfigService.InitializeAsync`:

```csharp
DailyRewardInstaller.RegisterConfig(dataConfig);
```

After `IDataConfigService` and `IDatasaveService` are registered, register the host economy adapter as `IResourceWallet`, optionally register an `IRewardClock`, then call:

```csharp
DailyRewardInstaller.Install();
```

Register a host `IResourceWallet`. Its `ResourceGrantRequest.TransactionId` must be idempotent: an already-processed transaction succeeds without granting again. This is required because an economy transaction and a local file save cannot be one atomic operation.

This legacy overload retains `scheduleId:cycle:entryId` transaction IDs and its weaker
wallet contract. A failed claim-save leaves Daily's loaded state unchanged, but a legacy
wallet may already have granted; the package cannot recover an unrecorded grant across
midnight. It does not wrap an arbitrary bool wallet into a strict gateway.

## Strict bundle delivery

Use the overload `DailyRewardModel(schedule, save, IRewardDeliveryGateway gateway, clock, saveKey)`
or `DailyRewardInstaller.Install(schedule, save, gateway, clock, saveKey)`.
The host supplies durable `IRewardDestination` owners through Economy's `RewardDeliveryGateway`.
Each owner must persist reward effects and transaction fingerprint in the same owner save.
These APIs require the coordinated, unreleased Economy reward source. Published dependency
versions will be updated together at the release gate; the old Economy tag does not supply them.

Each Daily entry declares either `resourceId`/`amount` or `bundle`, never both:

```json
{
  "scheduleId": "daily-seven",
  "revision": "1",
  "cycleMode": "Repeat",
  "resetProgressAfterMissedDay": true,
  "rewards": [{
    "id": "day-1",
    "day": 1,
    "bundle": {
      "schemaVersion": 1,
      "resolverVersion": "fixed-v1",
      "entries": [
        { "entryId": "coins", "kindId": "currency", "targetId": "currency.coin", "amount": 100 },
        { "entryId": "card", "kindId": "item", "targetId": "item.demo-card", "amount": 1 }
      ]
    }
  }]
}
```

Bundles are concrete resolved rewards, not a random resolver protocol. Known kinds and
targets are validated by the gateway before the first delivery. Strict request IDs use
Economy's versioned length-prefixed identity with save key, daily, schedule ID, cycle,
and Daily entry ID. Revision and UTC date do not create a new reward identity.

Save version 2 embeds the active schedule, original pending date/cycle/revision,
intended next transition, fixed payload and Economy journal in one Daily document.
Every persistence callback uses a detached candidate. Final progress and Completed
journal persist together. `Claim()` resumes pending work before evaluating the current
date or configuration, so a restart across midnight finishes the original claim date.
It reports `Pending` plus `Reason`/`Retryable`, and `Busy` for synchronous reentry.
`GetState().HasPendingClaim` exposes retry; no claim is reported successful before the
final save. The service is synchronous/main-thread-only, not thread-safe.

Version 1 saves preserve schedule ID, cycle, next index and last-claim date; they cannot
reconstruct a historical revision or a legacy torn grant. A changed schedule ID or invalid
counter requires an explicit host migration. Changed reward content/order/removals under
the same identity keep the saved active snapshot for the current cycle and adopt the new
revision at the next cycle. StopAfterLastDay never silently restarts. No progress is reset
just because configuration changed. Keep save/owner backups from the same generation;
independently rolling back an owner receipt ledger is outside recovery guarantees.

`DailyRewardClaimResult.Reward` remains available for legacy entries.
Bundle success instead exposes `Bundle`; do not assume `Reward.Value` exists.
Existing `IDailyRewardFeedback` implementations remain valid. Optional
`IDailyRewardBundleFeedback` receives bundle success; the sample audio adapter supports both.

## Host UI

Implement `IDailyRewardView` in any host-side MonoBehaviour. It may inherit `Dreamy.UI.UIPanel`, use UGUI, UI Toolkit, or a custom UI stack. Create a `DailyRewardPresenter` with `ServiceLocator.Get<IDailyRewardService>()`, call `Show()` after the view is ready, and call `Dispose()` in `OnDestroy`.

Audio is host-owned. Pass an optional `IDailyRewardFeedback` adapter to the presenter; the UIPanel sample includes an Audio adapter without adding an Audio dependency to runtime.

Prefab variants remain in the host project. No package type references a prefab, `MonoBehaviour`, or visual component.

## Schedule JSON

The `Daily Reward Feature` integration folder is a single copy-ready host folder. Copy it to the game's feature folder, then copy `Resources/DataConfig/dailyRewardSchedule.json` to the host DataConfig location. Reward days must start at 1 and be consecutive. Legacy `resourceId` uses the shared `category.name` convention. The optional `Daily Reward Bundle` sample adds a durable fake-item owner and alternative JSON without a second UI/service. Select one schedule JSON, not both.

`SystemRewardClock` uses device UTC time. Register a server-backed `IRewardClock` for authoritative live-service rewards.

## Production integration and presenter lifecycle

The editable integration entry point is `DailyRewardFeatureInstaller` in Samples~. Runtime `DailyRewardInstaller` remains available for custom UI; games using the supplied views call only the feature installer. All presenters implement the engine-independent `IPanelPresenter` lifecycle in `Dreamy.UI.Presentation`.

```csharp
DailyRewardFeatureInstaller.RegisterConfig(dataConfig); // Before dataConfig.InitializeAsync.
// After config/save/wallet readiness, using the same factory as other features:
DailyRewardFeatureInstaller.Install(factory, config, save, wallet, clock);
// Or reuse a host-owned service: DailyRewardFeatureInstaller.Install(factory, service);
```

Dependencies in this example belong to the composition root. No installer creates an in-memory wallet/save fallback. Model/service own rewards and checkpoints; views only render state and emit intent. Add direct asmdef references to the integration assembly and Dreamy.UI.Presentation wherever their APIs are used.

After assigning the shared factory to the scene's PanelManager, any caller can open `DailyRewardPanel` with Show/Transition by address, or Show with a prefab. Each opening creates one presenter; close, disable, destroy or failed show release it. Cached reopen creates a fresh presenter. No per-feature controller is required.

Sandbox validation: `python3 LocalPackages/com.dreamy.feature.settings/Tests~/validate-settings.py --shop --features`. This compiles runtime/integration/sample assemblies against their declared references and runs pure managed model/presenter regressions. Unity scene/coroutine/raycast lifecycle still requires Editor/PlayMode validation.

## Cài package

Dùng Unity 6000.0 trở lên. Sandbox đã tham chiếu package bằng `file:../LocalPackages/com.dreamy.feature.daily-reward`. Project khác dùng Package Manager > + > Install package from disk và chọn package.json, hoặc Git URL của repository nội bộ. Cài cả dependency Dreamy/Git vào manifest của game; version dependency không tự cấu hình registry riêng.

Dependency trực tiếp theo package.json:

- `com.dreamy.core` (1.1.2)
- `com.dreamy.dataconfig` (0.2.0)
- `com.dreamy.datasave` (0.2.0)
- `com.dreamy.audio` (0.1.0)
- `com.dreamy.feature.economy` (0.1.0 metadata; strict source release pending)
- `com.dreamy.feature` (0.1.0)
- `com.dreamy.ui` (0.2.0)

