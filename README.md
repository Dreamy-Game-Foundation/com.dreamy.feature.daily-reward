# Dreamy Daily Reward

`com.dreamy.daily-reward` is an MVP feature package. It owns schedule evaluation and saved claim progress; the host owns its UI, economy transaction, audio, art, and localization.

## Installation

Add the package and dependencies to the host manifest. During bootstrap, register the schedule before `IDataConfigService.InitializeAsync`:

```csharp
DailyRewardInstaller.RegisterConfig(dataConfig);
```

After `IDataConfigService` and `IDatasaveService` are registered, register the host economy adapter as `IRewardGrantService`, optionally register an `IRewardClock`, then call:

```csharp
DailyRewardInstaller.Install();
```

Register a host `IResourceWallet`. Its `ResourceGrantRequest.TransactionId` must be idempotent: an already-processed transaction succeeds without granting again. This is required because an economy transaction and a local file save cannot be one atomic operation.

## Host UI

Implement `IDailyRewardView` in any host-side MonoBehaviour. It may inherit `Dreamy.UI.UIPanel`, use UGUI, UI Toolkit, or a custom UI stack. Create a `DailyRewardPresenter` with `ServiceLocator.Get<IDailyRewardService>()`, call `Show()` after the view is ready, and call `Dispose()` in `OnDestroy`.

Audio is host-owned. Pass an optional `IDailyRewardFeedback` adapter to the presenter; the UIPanel sample includes an Audio adapter without adding an Audio dependency to runtime.

Prefab variants remain in the host project. No package type references a prefab, `MonoBehaviour`, or visual component.

## Schedule JSON

Copy the sample `dailyRewardSchedule.json` to `Assets/Resources/DataConfig/`. `scheduleId` changes intentionally reset local progress. Reward days must be unique, start at 1, and be consecutive. `resourceId` uses the shared `category.name` convention: `currency.gold`, `currency.gems`, and `item.chest`.

`SystemRewardClock` uses device UTC time. Register a server-backed `IRewardClock` for authoritative live-service rewards.
