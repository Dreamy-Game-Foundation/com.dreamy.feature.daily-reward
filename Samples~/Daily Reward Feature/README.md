# Daily Reward Feature

Import this sample and move the entire folder to the host game's feature folder. `DailyRewardPanel.prefab` and `DailyRewardItem.prefab` are variants of the base feature prefabs; keep `com.dreamy.feature` and `com.dreamy.ui` installed. Inspect the serialized Buttons, status TMP label, reward container, reward-item prefab, and optional `DailyRewardAudioFeedback` IDs after import.

Copy `Resources/DataConfig/dailyRewardSchedule.json` to the host DataConfig location. In the host installer, call `DailyRewardInstaller.RegisterConfig(dataConfig)` before config initialization. After registering config and save services, register `new InMemoryResourceWallet()` as `IResourceWallet`, then call `DailyRewardInstaller.Install()`.

Replace the in-memory wallet with the host economy service in production. Resource IDs use `category.name`; the shared defaults are `currency.coin` and `currency.gem`.
