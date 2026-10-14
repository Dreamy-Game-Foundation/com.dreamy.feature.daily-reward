# Daily Reward Bundle

Import this optional owner/config sample together with **Daily Reward Feature** for its UI.
Choose one `Resources/DataConfig/dailyRewardSchedule.json`; do not leave both copies active.
This sample does not contain a second prefab or a second Daily service.

Use the bundle JSON here instead of the currency-only JSON. Compose the two concrete
owners in the host bootstrap, then use the gateway overload of the existing feature installer:

```csharp
var wallet = new DatasaveResourceWallet(save, "daily-demo-currency");
var items = new DemoItemDestination(save);
var gateway = new RewardDeliveryGateway(new IRewardDestination[] { wallet, items });
DailyRewardFeatureInstaller.Install(factory, config, save, gateway, clock, "daily-bundle-demo-save", feedback);
```

Required namespaces are `Dreamy.Economy`, `Dreamy.Economy.Rewards`, and
`Dreamy.Feature.DailyReward.Integration`. The host composition assembly references
both imported sample assemblies and the packages whose APIs it calls.

Set `items.SimulateUnavailable = true` before claiming to demonstrate a real Pending result
after currency delivery. Set it back to false and claim again to resume the original
bundle without duplicating currency. Recreate owners and Daily with the same save keys
to demonstrate restart recovery, including across UTC midnight.

`item.demo-card` is a fixed fake item, not a mystery-card resolver or production inventory.
The host owns real item validation, selection, persistence and durable delivery receipts.
Do not adapt `InMemoryResourceWallet` into a strict owner.
