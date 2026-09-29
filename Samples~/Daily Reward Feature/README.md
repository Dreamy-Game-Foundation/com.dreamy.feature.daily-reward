# Daily Reward Feature

Copy this entire folder to the host game's feature folder. Create a host prefab from the `FeaturePanel` and `FeatureItem` base prefabs, then attach `DailyRewardPanel`, `DailyRewardRewardItem`, and `DailyRewardController`. Assign two Buttons, a status TMP label, a reward container, a reward-item prefab, and optional `DailyRewardAudioFeedback` IDs.

Copy `Resources/DataConfig/dailyRewardSchedule.json` to the host DataConfig location. In the host installer, call `DailyRewardInstaller.RegisterConfig(dataConfig)` before config initialization. After registering config and save services, register `new InMemoryResourceWallet()` as `IResourceWallet`, then call `DailyRewardInstaller.Install()`.

Replace the in-memory wallet with the host economy service in production. Resource IDs use `category.name`, such as `currency.gold`, `currency.gems`, and `item.chest`.
