# Dreamy Daily Reward

`com.dreamy.feature.daily-reward` is an MVP feature package. It owns schedule evaluation and saved claim progress; the host owns its UI, economy transaction, audio, art, and localization.

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

The `Daily Reward Feature` integration folder is a single copy-ready host folder. Copy it to the game's feature folder, then copy `Resources/DataConfig/dailyRewardSchedule.json` to the host DataConfig location. `scheduleId` changes intentionally reset local progress. Reward days must be unique, start at 1, and be consecutive. `resourceId` uses the shared `category.name` convention: `currency.gold`, `currency.gems`, and `item.chest`.

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
