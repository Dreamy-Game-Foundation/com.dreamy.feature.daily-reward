# Daily Reward Feature

Import sample rồi chuyển nguyên thư mục vào phần feature của game. DailyRewardPanel
và DailyRewardItem là prefab variant của nền Feature; cài Dreamy Feature và UI.
Kiểm tra Button, TMP label, container, row prefab và ID audio sau import.

Config nguồn của sample này là currency-only legacy. Đăng ký config trước
InitializeAsync, tạo config/save/wallet ở root rồi cài feature. Wallet in-memory
chỉ dành cho thử nghiệm; game dùng wallet bền vững hoặc adapter tới owner của mình.
Resource ID theo category.name, mặc định currency.coin và currency.gem.

Để nhận bundle nhiều owner có recovery, dùng Daily Reward Bundle và overload gateway.
Panel chung hiển thị các entry bundle và lý do Pending/lỗi; adapter audio hỗ trợ
cả legacy và bundle. Chỉ giữ một config tại key Resources/DataConfig tương ứng.
Bản import trong sandbox có thể dùng config bundle đã chọn thay cho config legacy.

Panel phân biệt Busy, Pending/retry, yêu cầu migration, cooldown và lịch hoàn tất.
StopAfterLastDay đã xong hiện All rewards claimed; các dòng giữ Claimed, không
hiện ngày năm 0001. Service/model sống ở root qua các lần mở panel.

## Tích hợp vào game

Dùng DailyRewardFeatureInstaller khi dùng view đi kèm. Runtime
DailyRewardInstaller dành cho UI tự viết:

```csharp
DailyRewardFeatureInstaller.RegisterConfig(dataConfig); // Trước dataConfig.InitializeAsync.
// Sau khi config/save/wallet sẵn sàng, dùng factory chung của các tính năng:
DailyRewardFeatureInstaller.Install(factory, config, save, wallet, clock);
// Hoặc dùng lại service do game sở hữu: DailyRewardFeatureInstaller.Install(factory, service);
```

Dependencies do composition root cung cấp; installer không tự tạo fallback save
hoặc wallet. Model quản lý reward/checkpoint; view chỉ render và gửi yêu cầu.
Gán factory chung cho PanelManager rồi mở panel bằng Show/Transition theo address
hoặc prefab. Mỗi lần mở tạo một presenter; đóng/disable/destroy/Show lỗi giải phóng
presenter, mở lại cache tạo presenter mới. Không tạo thêm controller cùng bind view.

## Contract và kiểm chứng

Presenter kế thừa Dreamy.Feature.FeaturePresenter và triển khai IPanelPresenter.
View dùng IFeatureView<TState> qua IDailyRewardView. Thêm reference trực tiếp tới
Dreamy.Feature.Runtime, Dreamy.UI.Presentation và assembly tích hợp ở nơi dùng API.
Source và bản import đã có reference Feature.

Lệnh harness sandbox: `python3 LocalPackages/com.dreamy.feature.settings/Tests~/validate-settings.py --shop --features`.
Harness managed không thay kiểm chứng Unity scene, coroutine, raycast và vòng đời
panel thực tế. Giữ nguyên GUID `.meta` khi đồng bộ source/imported sample.

## Resource feedback và bố cục

Import thêm Resource Holders của Economy và Basic Feedback của Feedback để giữ các reference prefab/feedback asset. DailyRewardPanel phát resource feedback khi claim thành công, dùng rewardHolders và rewardFeedback giống Shop. Truyền feedbackHost vào DailyRewardFeatureInstaller.Install để dùng FeedbackHost của game. Holder trong panel tắt autoRefresh để balance đổi sau hiệu ứng; bundle chỉ phát cho entry currency và bỏ qua item do owner khác quản lý. Đóng panel dừng hiệu ứng đang chạy.

Cấu hình số cột, kích thước ô và khoảng cách trực tiếp trên GridLayoutGroup trong Inspector; panel không tự điều chỉnh grid khi chạy. Title, status và reward item dùng chữ lớn hơn với auto-size có giới hạn.
