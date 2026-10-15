# Dreamy Daily Reward

Package quản lý lịch thưởng và tiến trình nhận đã lưu theo mô hình MVP. Game sở
hữu UI, giao dịch economy, audio, hình ảnh và bản địa hoá.

## Cài đặt

Cài package cùng dependency trong package.json. Đăng ký lịch trước khi gọi
IDataConfigService.InitializeAsync:

```csharp
DailyRewardInstaller.RegisterConfig(dataConfig);
```

Sau khi config/save sẵn sàng, đăng ký wallet của game dưới IResourceWallet,
tuỳ chọn đăng ký IRewardClock, rồi cài service:

```csharp
DailyRewardInstaller.Install();
```

Đây là đường legacy. Wallet cần xử lý TransactionId đã nhận mà không cấp lại.
Identity cũ giữ dạng scheduleId:cycle:entryId. Nếu grant đã thành công nhưng save
claim lỗi, Daily giữ nguyên state đã load nhưng wallet có thể đã cấp thưởng.
Không có bảo đảm recovery hồi tố cho grant chưa được ghi nhận qua midnight;
không biến bool wallet bất kỳ thành strict gateway.

## Nhận bundle có recovery

Dùng DailyRewardModel(schedule, save, gateway, clock, saveKey) hoặc overload gateway
của DailyRewardInstaller.Install. Game cung cấp IRewardDestination qua
RewardDeliveryGateway; mỗi owner phải lưu effect và receipt fingerprint trong cùng
save document. Cài đúng phiên bản Economy có các API Rewards theo dependency hiện tại.

Mỗi entry khai báo resourceId/amount hoặc bundle, không khai báo đồng thời:

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

Bundle chứa reward cụ thể đã resolve. Gateway kiểm kind/target trước delivery.
Request ID dùng encoding length-prefixed của Economy: save key, daily, schedule ID,
cycle và entry ID. Revision hoặc ngày UTC không tạo identity mới để cấp lại reward.

Save v2 chứa active schedule, ngày/kỳ/revision gốc của pending claim, transition
kế tiếp, payload cố định và journal trong cùng Daily document. Callback persistence
lưu detached candidate; tiến trình cuối và journal Completed được commit cùng nhau.
Claim phục hồi pending trước khi xét ngày/config mới; qua midnight vẫn hoàn tất
claim với ngày gốc. Kết quả có Pending, Reason/Retryable hoặc Busy khi gọi lồng.
HasPendingClaim cho biết còn việc cần retry; chỉ báo thành công sau final save.
Service đồng bộ trên main thread, không thread-safe.

Trước khi gọi resolver/owner, payload intent phải khớp fingerprint đã lưu.
Dữ liệu sai trả InvalidJournal; payload thay đổi trả PayloadMismatch, không grant.
Sau Resolve, journal là authority payload; bản trùng trong PendingClaim chỉ giữ
để tương thích save và không được đọc khi delivery. IsCompleted xác định lịch
StopAfterLastDay đã xong; các dòng giữ Claimed và sample hiện All rewards claimed.

Save v1 giữ schedule ID, cycle, next index và ngày claim cuối; không dựng lại được
revision lịch sử hoặc legacy grant bị đứt bước lưu. Thay schedule ID hay counter
không hợp lệ cần migration rõ ràng. Thay nội dung/thứ tự reward cùng identity giữ
snapshot chu kỳ hiện tại, áp dụng config mới ở chu kỳ tiếp theo. StopAfterLastDay
không tự khởi động lại. Backup owner/journal phải cùng generation; restore ledger
riêng nằm ngoài bảo đảm recovery.

DailyRewardClaimResult.Reward vẫn dành cho entry legacy. Bundle thành công trả
Bundle; không mặc định Reward.Value tồn tại. IDailyRewardFeedback cũ vẫn dùng
được; IDailyRewardBundleFeedback tuỳ chọn nhận feedback cho bundle.

## UI và sample của game

Triển khai IDailyRewardView trên MonoBehaviour/UI riêng, có thể kế thừa UIPanel.
Tạo DailyRewardPresenter với service của root, gọi Show khi view sẵn sàng và Dispose
khi đóng/teardown. Audio do adapter IDailyRewardFeedback của game cung cấp.
Runtime không tham chiếu prefab hay component hiển thị.

Import Daily Reward Feature cho panel/row. Daily Reward Bundle cung cấp owner item
mẫu và config thay thế, không tạo service/UI thứ hai. Chỉ giữ một JSON tại
Resources/DataConfig/dailyRewardSchedule.json; ngày bắt đầu từ 1 và liên tiếp.
SystemRewardClock dùng UTC của thiết bị; không phải đồng hồ chống gian lận.

## Tích hợp vào game và vòng đời presenter

Dùng DailyRewardFeatureInstaller trong sample khi dùng view đi kèm;
DailyRewardInstaller dành cho UI tự viết. Dependencies được tạo ở composition root:

```csharp
DailyRewardFeatureInstaller.RegisterConfig(dataConfig); // Trước dataConfig.InitializeAsync.
// Sau khi config/save/wallet sẵn sàng, dùng factory chung của các tính năng:
DailyRewardFeatureInstaller.Install(factory, config, save, wallet, clock);
// Hoặc dùng lại service do game sở hữu: DailyRewardFeatureInstaller.Install(factory, service);
```

Presenter kế thừa FeaturePresenter và triển khai IPanelPresenter; view kế thừa
IFeatureView<TState> qua IDailyRewardView. Thêm reference trực tiếp tới
Dreamy.Feature.Runtime, Dreamy.UI.Presentation và assembly tích hợp ở nơi dùng API.

Gán factory chung cho PanelManager. Mỗi lần mở panel có một presenter; đóng,
disable, destroy hoặc Show lỗi sẽ giải phóng presenter; mở lại cache tạo presenter
mới. Service/model sống theo root, không tạo lại khi mở panel.

## Kiểm chứng và dependency

Dependency và version chính xác nằm trong package.json; thêm các dependency Git
vào manifest game khi registry không cung cấp. Sandbox dùng local file dependencies.

Có thể chạy `python3 LocalPackages/com.dreamy.feature.settings/Tests~/validate-settings.py --shop --features`
để compile managed assemblies và regression theo harness. Kiểm tra scene, coroutine,
raycast, import và vòng đời UI bằng Editor/PlayMode riêng. Kết quả source không
thay thế kiểm chứng migration, build/device hoặc consumer cụ thể.
