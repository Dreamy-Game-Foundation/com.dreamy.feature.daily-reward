# Dreamy Daily Reward

Package thuộc Dreamy Game Studio. Hướng dẫn dưới đây mô tả cấu trúc, cách cài vào project và tích hợp ở root/scene.

## Cài package

Dùng Unity 6000.0 trở lên. Sandbox đã tham chiếu package bằng `file:../LocalPackages/com.dreamy.feature.daily-reward`. Project khác dùng Package Manager > + > Install package from disk và chọn package.json, hoặc Git URL của repository nội bộ. Cài cả dependency Dreamy/Git vào manifest của game; version dependency không tự cấu hình registry riêng.

Dependency trực tiếp theo package.json:

- `com.dreamy.core` (1.1.2)
- `com.dreamy.dataconfig` (0.2.0)
- `com.dreamy.datasave` (0.2.0)
- `com.dreamy.audio` (0.1.0)
- `com.dreamy.feature.economy` (0.1.0)
- `com.dreamy.feature` (0.1.0)
- `com.dreamy.ui` (0.2.0)

## Cấu trúc và asmdef

| Assembly | Reference | Phạm vi |
| --- | --- | --- |
| `Dreamy.DailyReward.Runtime` | Dreamy.Core.Runtime, Dreamy.DataConfig.Runtime, Dreamy.Datasave.Runtime, Dreamy.Economy.Runtime, Unity.Newtonsoft.Json | Runtime |

Trong asmdef của game, thêm assembly chứa API trực tiếp sử dụng. Code bootstrap reference thêm Core/DataConfig/Datasave/Economy theo nhu cầu; code async reference UniTask. Code gọi type sample reference assembly sample. Giữ Editor reference trong asmdef Editor-only.

## Cấu trúc và trách nhiệm

Runtime/Config chứa catalog; Contracts chứa service/view và adapter; Domain chứa quy tắc và state; Installation chứa installer; Persistence xử lý tiến trình lưu. Presentation (nếu có) nối service với view. Samples~ là integration được import vào Assets; game sở hữu UI, gameplay, localization và adapter SDK.

## Cài service ở GameInstaller

Dùng một DataConfig và Datasave dùng chung. Ghép đoạn dưới vào root async; không tạo lại các service trong panel. Cài và đăng ký IResourceWallet dùng chung trước feature; wallet giữ transaction ID ổn định.

```csharp
using Dreamy.Core;
using Dreamy.DataConfig;
using Dreamy.DailyReward;

// dataConfig: instance root đã tạo, chưa initialize.
DailyRewardInstaller.RegisterConfig(dataConfig);
await dataConfig.InitializeAsync(cancellationToken);
ServiceLocator.Register<IDataConfigService>(dataConfig);
// IDatasaveService và wallet (nếu cần) đã đăng ký trước đây.
IDailyRewardService service = DailyRewardInstaller.Install();
```

Với nhiều feature, gọi tất cả RegisterConfig trước một InitializeAsync, rồi mới gọi Install cho từng feature. JSON cần có đúng một Resources/DataConfig/dailyRewardSchedule.json. Root unregister IDailyRewardService khi teardown; dispose presenter/subscription theo lifecycle UI.

## Sử dụng và sample

DailyRewardController tạo presenter ở Start nhưng không tự init/show animation của UIPanel. Host chọn controller sample hoặc tự bind DailyRewardPresenter, không tạo cả hai. Gán reward container/item prefab và feedback audio. Schedule có ngày liên tiếp từ 1; đổi scheduleId sẽ reset tiến trình. Wallet cần idempotency khi claim retry vì grant và save là hai thao tác riêng. IRewardClock mặc định dùng UTC thiết bị; game online nên cung cấp clock server.

## Import sample

Mở Window > Package Manager, chọn Dreamy Daily Reward > Samples > Import. Unity chép vào Assets/Samples/Dreamy Daily Reward/0.1.0/. Chuyển cả folder nếu tùy biến, giữ .meta và reference prefab; không giữ bản script/asmdef hoặc Resources document trùng.

- **Daily Reward Feature**: nguồn `Samples~/Daily Reward Feature`.
  Assembly `Dreamy.Feature.DailyReward.Integration.Runtime` reference Dreamy.DailyReward.Runtime, Dreamy.Economy.Runtime, Dreamy.Core.Runtime, Dreamy.UI.Runtime, Dreamy.Audio.Runtime, Unity.TextMeshPro, UnityEngine.UI, UniTask.

## Addressables Group và class address

1. Lưu prefab/variant của game tại Assets/_Project/Prefabs/Panel/DailyRewardPanel.prefab. Với UIPanel, root phải có subclass tương ứng.
2. Mở Window > Asset Management > Addressables > Groups; tạo settings nếu chưa có.
3. Tạo group UI Panels và kéo prefab vào group.
4. Đặt cột Address thành Panel/DailyRewardPanel.prefab.
5. Tạo class dùng chung trong game:

```csharp
public static class PanelAddress
{
    public const string Home = "Panel/HomePanel.prefab";
    public const string Current = "Panel/DailyRewardPanel.prefab";
}
```

Đường dẫn asset trên disk và address là hai giá trị riêng. Address do bạn đặt, constant phải khớp chính xác cột Address. Tên group không phải key tải. HomePanel là ví dụ subclass do game tự tạo.

Scene cần Canvas có PanelManager và EventSystem/input module. Chờ root cài service xong. Các lệnh sau nằm trong method async UniTask; asmdef reference Dreamy.UI.Runtime, UniTask và assembly chứa type panel.

```csharp
var panel = await PanelManager.Instance.Create<DailyRewardPanel>(PanelAddress.Current);
var presenter = new DailyRewardPresenter(
    ServiceLocator.Get<IDailyRewardService>(), panel);
presenter.Show();
await panel.Show();
// Đóng từ code game:
await PanelManager.Instance.Close<DailyRewardPanel>();
```

Host giữ một presenter cho mỗi panel instance, dispose lúc teardown, bind/render lại khi mở panel cache. Không chạy đồng thời controller sample và presenter khác trên cùng panel. PanelManager không tự cài service feature.

Build Addressables content cho target trước khi thử player. AssetLoader cache prefab; đóng panel không tự unload cache. Chỉ unload sau khi mọi instance/consumer đã kết thúc.
