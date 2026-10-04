# Daily Reward Feature

Sample của Dreamy Daily Reward. Import từ Window > Package Manager > Dreamy Daily Reward > Samples > Import. Unity chép nội dung vào Assets/Samples/Dreamy Daily Reward/0.1.0/Daily Reward Feature/.

## Cấu trúc và tích hợp

Giữ nguyên folder, .meta, asmdef và reference prefab khi chuyển vào project. Chỉ giữ một bản script/asmdef và một JSON cho mỗi key Resources/DataConfig. Bootstrap config/save/wallet/audio tại GameInstaller trước khi bật UI, theo [README package](../../README.md). Link tương đối này dùng trong source package; sau import, mở README package từ Package Manager.

## Sử dụng và sample

DailyRewardController tạo presenter ở Start nhưng không tự init/show animation của UIPanel. Host chọn controller sample hoặc tự bind DailyRewardPresenter, không tạo cả hai. Gán reward container/item prefab và feedback audio. Schedule có ngày liên tiếp từ 1; đổi scheduleId sẽ reset tiến trình. Wallet cần idempotency khi claim retry vì grant và save là hai thao tác riêng. IRewardClock mặc định dùng UTC thiết bị; game online nên cung cấp clock server.


Assembly Dreamy.Feature.DailyReward.Integration.Runtime reference Dreamy.DailyReward.Runtime, Dreamy.Economy.Runtime, Dreamy.Core.Runtime, Dreamy.UI.Runtime, Dreamy.Audio.Runtime, Unity.TextMeshPro, UnityEngine.UI, UniTask.

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
