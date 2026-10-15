# Daily Reward Bundle

Import sample owner/config này cùng Daily Reward Feature để dùng UI có sẵn.
Chọn một Resources/DataConfig/dailyRewardSchedule.json, không bật hai bản cùng key.
Sample không tạo prefab hoặc service Daily thứ hai. Config gồm lịch 7 ngày, mỗi
ngày một bundle coin + gem + card demo; ngày 7 có milestone lớn hơn.

## Ghép các owner tại root

Thay JSON currency-only bằng config bundle, rồi truyền gateway vào feature installer:

```csharp
var wallet = new DatasaveResourceWallet(save, "daily-demo-currency");
var items = new DemoItemDestination(save);
var gateway = new RewardDeliveryGateway(new IRewardDestination[] { wallet, items });
DailyRewardFeatureInstaller.Install(factory, config, save, gateway, clock, "daily-bundle-demo-save", feedback);
```

Dùng namespaces Dreamy.Economy, Dreamy.Economy.Rewards và
Dreamy.Feature.DailyReward.Integration. Assembly composition tham chiếu cả hai
assembly sample và các package có API được gọi.

Đặt items.SimulateUnavailable=true trước claim để thử Pending sau khi currency đã
được giao. Tắt cờ rồi claim lại: bundle gốc tiếp tục, không cộng currency lần hai.
Tạo lại tất cả owner và Daily với cùng save key để thử restart, kể cả qua UTC midnight.

item.demo-card là item cố định để minh hoạ, không phải inventory thật hay resolver
mystery card. Game chịu trách nhiệm chọn/validate/lưu item và durable receipt.
Không biến InMemoryResourceWallet thành strict owner bằng cách bọc API bool.

## Reward do authority của game resolve

JSON sample đã cụ thể, không cần resolver. Với requiresResolution=true, root tạo
service cùng resolver rồi đăng ký với factory chung:

```csharp
var daily = new Dreamy.DailyReward.DailyRewardModel(
    config, save, gateway, clock, "daily-bundle-demo-save", hostResolver);
DailyRewardFeatureInstaller.Install(factory, daily, feedback);
```

hostResolver triển khai IDailyRewardBundleResolver. Contract hiện đồng bộ: trả
kết quả cụ thể hoặc Pending, không chặn Unity thread để chờ network. Intent lưu
trước callback; cùng request ID phải replay cùng outcome dù lần lưu resolved bị lỗi.
Thiếu authority giữ Pending, không tự roll card thay thế ở client.

Trước Resolve, payload intent gốc được kiểm fingerprint. Sau Resolve, recovery đọc
journal, không gọi resolver lại. Receipt của owner chặn cấp lặp nếu acknowledgement
của feature lỗi. Bundle không atomic toàn bộ: currency có thể đã nhận trong khi
item còn Pending.

## Kiểm tra sau import

Thử item unavailable, đóng/mở và retry; recreate toàn bộ owner cùng save key rồi
kiểm currency chỉ được cấp một lần. Thử StopAfterLastDay: tất cả dòng giữ Claimed,
không hiện ngày năm 0001. Đồng bộ integration source và bản import, giữ GUID prefab
và script. Không để hai JSON hoạt động cùng key DataConfig.
