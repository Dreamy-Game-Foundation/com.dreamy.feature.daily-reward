# Daily Reward Feature

Import this sample and move the entire folder to the host game's feature folder. `DailyRewardPanel.prefab` and `DailyRewardItem.prefab` are variants of the base feature prefabs; keep `com.dreamy.feature` and `com.dreamy.ui` installed. Inspect the serialized Buttons, status TMP label, reward container, reward-item prefab, and optional `DailyRewardAudioFeedback` IDs after import.

Copy `Resources/DataConfig/dailyRewardSchedule.json` to the host DataConfig location. In the host installer, call `DailyRewardInstaller.RegisterConfig(dataConfig)` before config initialization. After registering config and save services, register `new InMemoryResourceWallet()` as `IResourceWallet`, then call `DailyRewardInstaller.Install()`.

Replace the in-memory wallet with the host economy service in production. Resource IDs use `category.name`; the shared defaults are `currency.coin` and `currency.gem`.

This is the currency-only legacy route. For recoverable mixed-owner delivery, import
the optional **Daily Reward Bundle** sample and follow its composition example instead.
The same panel renders every bundle entry and reports Pending/failure reasons.
`DailyRewardAudioFeedback` supports both the legacy and optional bundle feedback contracts.
Use only one schedule JSON at the DataConfig resource address.

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
