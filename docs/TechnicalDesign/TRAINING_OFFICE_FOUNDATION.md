# Training Office Foundation

> Cập nhật MVP: [vòng chơi, cổng chính, bị bắt và retry (thiết kế đề xuất)](MVP_ESCAPE_LOOP.md).
> Phần dưới ghi lại nền tảng trước MVP; mô tả “chưa có Main Gate/Game Over” đã được thay thế bởi tài liệu trên.


Scene: `Assets/_Project/Scenes/TrainingOffice_Prototype.unity`.
Campus: cửa tại X=16, Z=53.9; đứng khoảng Z=51.2..52 và nhấn E.
Nhánh đường nối từ đường phía Bắc Z=48; biển PHÒNG ĐÀO TẠO ở trên cửa.
Chỉ vào sau ba khám phá Campus (sân trường, sơ đồ, biển hướng); không giải lại
Floor 01. Mở Campus trực tiếp thì dùng F5 → Continue Campus để nhận snapshot
hành trình trước đó. Đi ngang không tự chuyển Scene.

Office có sảnh, quầy tiếp nhận, bảng thông báo bên trái, hai bàn làm việc/máy
tính, ghế chờ, tủ hồ sơ và đèn. Dùng vật liệu có sẵn, hình học lưu trong Scene.
Actor rig được sao từ Campus và dùng nguyên Player/Animator/Camera/Inventory;
tham chiếu nội bộ được Unity remap, không tham chiếu Player ở Scene khác.

Khám phá bằng cùng QuestManager và modal OnGUI hiện có:
1. E tại bảng thông báo phía trái, khoảng (-6.8, 7).
2. F tại tài liệu quầy tiếp nhận, đứng khoảng (-3, 10).
3. E tại máy tính bàn bên phải, đứng khoảng (4, 16).
Máy tính yêu cầu hai thông tin trước đó. HUD chuyển sang Đến cổng chính;
Main Gate chưa có Scene, không chuyển khu vực hoặc kết thúc game.
J thêm ba mục tiêu sau 17 mục tiêu Floor và ba mục tiêu Campus.
Esc đóng thông tin; input gate cũ chặn gameplay phía sau.
Cửa sảnh Office dùng E trở lại Campus, giữ thông tin đã khám phá trong phiên.

JourneySession/SceneLoader hiện có tải Single. Không autosave khi chuyển Scene.
F5 → Lưu khu vực hiện tại / Continue khu vực hiện tại. Slot mới phiên bản 1:
`Application.persistentDataPath/trainingoffice-v1.json`, chứa snapshot Floor,
Campus facts, Office facts, vị trí và yaw. Ghi đè cần xác nhận, ghi qua pending
và giữ previous. Floor/Campus giữ hai slot riêng. Save Campus mới có thêm
Office facts tùy chọn để giữ tiến độ khi quay lại; save Campus cũ vẫn đọc được.
Continue từ F5 có thể chọn các slot khu vực khác; New Game vẫn quay về Floor.
Thiếu/hỏng/sai phiên bản bị từ chối trước khi đổi Scene; vị trí khôi phục có
kiểm tra va chạm và dùng spawn dự phòng nếu cần. Không thay đổi FloorSave V1.

Kiểm thử nhỏ bằng Unity 6000.6.5f1 trên bản D dùng cùng Assets với main;
log trong `D:/Game3D-01/TempTests/TrainingOffice_20261113/`.
Không chạy suite Floor/AI cũ. Fixture save sao riêng trên D, không ghi save thật.
Biên dịch, E/F, điều kiện vào, Single loading, slot Office, Continue chéo Scene,
giữ nguyên hai file save cũ và lỗi save được kiểm tra tự động.
Hình ảnh, bố cục, cảm giác camera và đi lại tự nhiên: người dùng nghiệm thu.
Editor Search indexing và cảnh báo Animator AnyState→Run cũ giữ nguyên.
