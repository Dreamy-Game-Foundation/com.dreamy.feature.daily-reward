# Daily Reward UIPanel sample

Create a host prefab with `DailyRewardSamplePanel`, two Buttons, a status TMP label, a container, and a `DailyRewardSampleRewardItem` prefab. Attach `DailyRewardSampleController` to the same host root.

In the host installer, call `DailyRewardInstaller.RegisterConfig(dataConfig)` before config initialization. After registering config and save services, register `new InMemoryResourceWallet()` as `IResourceWallet`, then call `DailyRewardInstaller.Install()`.

Replace the in-memory wallet with the host economy service in production. Resource IDs use `category.name`, such as `currency.gold`, `currency.gems`, and `item.chest`.
