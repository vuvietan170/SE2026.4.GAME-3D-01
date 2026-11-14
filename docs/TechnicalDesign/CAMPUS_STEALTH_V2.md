# Campus Stealth V2

> Cập nhật MVP: [vòng chơi, cổng chính, bị bắt và retry (thiết kế đề xuất)](MVP_ESCAPE_LOOP.md).
> Phần dưới ghi lại nền tảng trước MVP; mô tả “chưa có Main Gate/Game Over” đã được thay thế bởi tài liệu trên.


- `PatrolGuard` giữ GUID và các trường cấu hình V1; bổ sung Search, Return,
  vị trí cuối, truy đuổi tối đa 8 giây/22 m tính từ vị trí ban đầu.
  Tốc độ bảo vệ: tuần tra 1.5 m/s, truy đuổi 3.2 m/s.
- Chó dùng chung brain với `isDog=true`, nghe tiếng bước chân mô phỏng và báo
  động trực tiếp tới bảo vệ được gán trong Inspector. Không có bus toàn cục.
  Suspicious tương ứng Investigate; Alert tương ứng Bark/truy đuổi.
- Chạy phát tiếng xa hơn đi bộ; cúi giảm nghe và nhìn. Đứng yên không tạo
  tiếng bước chân. Vật cản rắn chặn nhìn, giảm phạm vi nghe xuống 35%.
- Mất tầm nhìn: không cập nhật vị trí Player; sau 3 giây chuyển Search,
  tìm tối đa 4 giây rồi về waypoint gần nhất. Chạm giới hạn truy đuổi buộc
  Return và có khoảng nghỉ khi trở lại tuyến.
- Chó tuần tra hình chữ nhật X=10..20, Z=16..20, gần tuyến bảo vệ cũ.
  `Dog_Visual_ReplaceWithModel` có thân, đầu, mõm, bốn chân, tai, đuôi;
  có thể thay riêng visual sau này. Chỉ root có CharacterController.
- Vòng cổ đổi xanh/vàng/đỏ theo AI; HUD có An toàn/Nghi ngờ/Phát hiện và
  thông báo chó sủa. Chưa có âm thanh, cắn, sát thương hoặc Game Over.
- `JourneySceneEntry.guards` gán cả hai NPC và bind Player bằng cơ chế cũ;
  `CampusStealthHUD` chỉ hiển thị, nhận hai tham chiếu Scene rõ ràng.
  Không sửa Quest, Save, Camera, Player hoặc Packages/ProjectSettings.

Kiểm tra Unity 6000.6.5f1 trên bản sao D có Assets bàn giao giống main:
20/20 kiểm tra nhỏ qua Play Mode, exit code 0. Bao gồm tuyến chó đi thực tế,
che khuất bằng tường Campus, tiếng động, cúi, trạng thái, mất dấu, Return,
báo động chó–bảo vệ và giới hạn thời gian truy đuổi. Không chạy bộ test cũ.
Log: `D:/Game3D-01/TempTests/StealthV2_20261113/FinalFocusedTests/`.
Harness tạm tắt PlayerController và đặt vị trí trong Play Mode để kiểm tra AI;
không lưu các thao tác đó vào asset. Giới hạn thời gian được kiểm tra bằng
timestamp cũ trong brain production. Không thay bằng AI giả.

Nghiệm thu thủ công: mở Campus, tiếp cận NPC ở sân phía đông, so sánh đi/chạy/cúi,
nấp sau tường X=6, Z=26..34 và quan sát mất dấu/tìm kiếm/trở lại tuần tra.
Kiểm tra tiếp HUD với J/Tab/F5 và cảm giác di chuyển trong cảnh thực tế.
Tránh vật cản là local steering, không có tìm đường NavMesh cho mê cung;
NPC chưa lưu trạng thái AI vào save và khởi động lại tuần tra khi tải Scene.
Lỗi Editor Search indexing và cảnh báo Animator AnyState→Run cũ không sửa.
