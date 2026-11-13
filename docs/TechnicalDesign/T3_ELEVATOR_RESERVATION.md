# T3 — Sảnh thang máy dự phòng

## Phạm vi

`Floor01_Prototype` hiện nằm ở cao độ tầng trệt. Lối thoát chính vẫn là
hành lang → sảnh T3 → cửa ra Campus; không thêm cầu thang, cao độ hoặc tầng mới.
Thang máy chỉ là hình học kiến trúc và nút gọi chưa hoạt động.

## Bố trí

- Root chỉnh sửa trong Scene: `T3_ElevatorLobby_Reserved`.
- Mặt cửa ở vách đông sảnh, tâm X=3.03, Z=66.5; mặt sàn giữ nguyên Y=0.
- Cửa đôi đóng, khung cửa, bảng `THANG MAY T3 / G`, bảng gọi và nút vàng.
- Vùng đứng gọi: tâm X=2, Y=1.5, Z=68.05, kích thước 1.5 × 3 × 1.4.
  Vùng này không giao với vùng E tại cửa ra T3 và không chiếm tim lối thoát X=0.
- Transform `RESERVED_SHAFT_4x4m_NO_GAMEPLAY` đánh dấu phần giếng dự phòng
  4 × 4 m ở X=5.2, Z=66.5, phía ngoài vách đông; chưa có cabin hay sàn tiếp cận.
- Vách đông được chia thành hai đoạn và một phần bịt riêng
  `Elevator_ShaftOpening_Infill_RemoveWhenEnabled`. Khi làm thang máy thật,
  thay phần bịt và xây dựng giếng, cabin, an toàn cửa; không phải phá lại hành lang.
  Các kích thước này chỉ là quy hoạch prototype, chưa phải thiết kế thi công.

## Tương tác

`ElevatorUnavailable` được gắn vào root mới và nhận tham chiếu Scene tường minh
tới `JourneySceneEntry`, `BuildingExit` và collider trigger vùng gọi.
Đứng cạnh bảng gọi và nhấn E sẽ hiện **Thang máy chưa khả dụng** trong 4 giây.
Rời vùng gọi thì ẩn lời nhắc. Journal, giao diện nhiệm vụ, menu F5, Inventory
và quá trình chuyển Scene chặn thao tác này. Cửa ra T3 được ưu tiên nếu sau này
các vùng tương tác bị đặt chồng nhau.

Component không di chuyển Player/cửa, không thêm SceneLoader, không cập nhật
Quest hay Inventory, không lưu dữ liệu. Save V1/Campus và điều kiện hoàn thành
17 mục tiêu để rời T3 giữ nguyên. Dùng lại vật liệu có sẵn; không thêm package.

## Kiểm chứng Unity 6000.6.5f1

Bản sao trên D có Assets bàn giao giống project chính; harness chỉ cài trong
bản sao, không gắn vào Scene chính. Không điều khiển Editor chính đang mở.

- Native Play probe: 13/13; E thật qua Input System, đúng thông báo, cửa đứng yên,
  không đổi Quest/Inventory/Scene, chặn J/Tab và giữ riêng tương tác cửa ra.
- Hồi quy lồng: Floor–Campus 81/81; Floor 01 Save/Quest/input 388/388.
  Kiểm tra camera tại sảnh qua 968 frame không phát hiện va chạm near plane.
- NUnit Edit Mode 110/110, Play Mode 33/33; không có bài bị bỏ qua.
- AssetDatabase kiểm tra 2 Scene, 28 GUID script và 0 Missing Script;
  166 GUID metadata không trùng; toàn bộ `.meta` cũ không đổi.
- Game View 1920×1080 được chụp và xem: biển không ngược, cửa/nút nhìn thấy,
  thông báo tiếng Việt đọc được. Ảnh không thay thế nghiệm thu điều khiển thủ công.
- Báo cáo/log/ảnh tại `D:/Game3D-01/TempTests/T3Elevator_20261113/`;
  bản Scene trước sửa tại `Original/Assets/_Project/Scenes/Floor01_Prototype.unity`.
- Các lần thử harness ban đầu có lỗi khôi phục InputSettings, bước đặt nhân vật
  chưa ổn định và thiếu namespace trong công cụ chụp ảnh; đã sửa ở harness.
  Không sửa Player, camera hoặc gameplay để vượt qua kiểm thử.

Mở Scene chính sau khi xử lý mọi Scene đang sửa chưa lưu trong Editor; đi cuối
hành lang qua sảnh, tìm cửa thang máy ở vách phải và nhấn E cạnh nút vàng.
Sau đó tiếp tục đến cửa ra T3; hoàn thành LAB vẫn là điều kiện để sang Campus.
Chưa có chuyển tầng, animation thang máy hay tương tác nút bằng chuột.
