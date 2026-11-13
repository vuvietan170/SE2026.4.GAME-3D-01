# Bootstrap và hành trình gameplay

Nhánh sửa: `fix/bootstrap-journey-integration`, từ `origin/dev` commit `f933595`
(fetch ngày 13/11/2026). Unity project root: `HUS_Escape/`, Unity `6000.6.5f1`.
Không tìm thấy AGENTS.md trong repository hoặc các thư mục cha áp dụng tại thời
điểm kiểm tra. Các thư mục/file chưa theo dõi có sẵn được giữ nguyên, không dùng
bản project lồng trong `SE2026.4.GAME-3D-01/` để sửa hoặc chạy test.

## Luồng và ownership

`Bootstrap → SceneLoader → MainMenu (Ready) → Start → SceneLoader → Floor01_Prototype (Playing)`.
Sau khi hoàn thành Floor01: `Floor01 → Campus → TrainingOffice → Campus`.
Tất cả tải bằng SceneLoader hiện có, backend mặc định Single; không thêm đường
LoadScene độc lập, không đổi state machine hoặc script execution order của Core.

- Bootstrap giữ GameBootstrap/SceneLoader/MainMenuFlow xuyên scene. MainMenuFlow
  mặc định và reference serialize trong Bootstrap đều trỏ Floor01.
- JourneySceneEntry.Start là coroutine: giữ input khi actor đã active và đợi
  loader hết Loading trước khi tạo/gắn JourneySession. Không timeout hoặc nhả
  reservation khi native load vẫn đang chạy. Nếu Core đã tồn tại nhưng không
  khả dụng thì từ chối, không tạo Core thay thế. Chơi trực tiếp scene gameplay
  vẫn có thể tạo một Core khi hoàn toàn chưa có Core.
- Tìm owner bao gồm object inactive để tránh tạo thêm owner khi tạm deactivate.
  Session chỉ khởi tạo trên Core Ready/Playing ổn định, đăng ký sự kiện một lần;
  gọi Initialize lặp không thêm handler. Attach từ chối trong Loading.
- Trong các chuyến đi tiếp theo, Session.Loaded gắn actor sau khi Core commit
  Playing và nhả reservation. Nếu Loaded đã Bind trước Start thì Start kết thúc
  ngay; nếu Start chạy trước thì nó đợi, không khôi phục tiến trình hai lần.
- Player, camera, inventory và quest component là scene-local. Session chỉ giữ
  snapshot/facts qua scene. Khi trở lại Campus dùng checkpoint cửa Office
  `(16, 1.08, 51)`, yaw 180. Spawn/restore vẫn qua kiểm tra vị trí an toàn có sẵn.
  Input gate nguồn được khôi phục khi unload hoặc tải bị từ chối; UI đích đóng
  modal và nhả gate sau khung hình an toàn như trước.

## Scene và test

Giữ nguyên sáu scene bật trong Build Settings: Bootstrap, MainMenu, SampleScene,
Floor01_Prototype, Campus_Prototype, TrainingOffice_Prototype. Không đổi GUID,
metadata, nội dung SampleScene hoặc Build Settings. SampleScene vẫn phục vụ
native loader tests. Sửa hai test catalog cũ còn thiếu TrainingOffice; test menu
kiểm tra Start trỏ Floor01/Playing và chỉ gửi một yêu cầu khi nhấn nhiều lần.

`tools/Integration/audit_journey.py` kiểm tra nguồn: GUID trùng, path/GUID của
Build Settings, destination Start và reference chính của ba JourneySceneEntry.
Audit AssetDatabase trong `JourneyIntegrationValidation.cs` mở cả sáu scene,
kiểm tra missing scripts, missing object reference và reference chéo scene.

`JourneyIntegrationProbe.cs` chạy từ Bootstrap qua UI Button và loader production,
không fake backend. Probe chủ động gọi bước đầu coroutine Start tại callback
SceneManager.sceneLoaded khi loader vẫn Loading, rồi để Start Unity chạy bình
thường để chứng minh thứ tự sớm cũng an toàn. Nó dùng một checkpoint Floor01
hoàn thành hợp lệ qua RestoreForTravel để cô lập việc chuyển khu vực khỏi bước
giải puzzle; không ghi/đọc save của người chơi. Các điểm khám phá Campus/Office
được gửi qua QuestManager.Signal. Probe kiểm tra bốn gameplay loads, inventory,
quest facts, spawn, camera trỏ player scene-local, cùng loader/session, input,
Time.timeScale, từ chối load thiếu scene và từ chối các yêu cầu lặp.

Bước thời gian probe cố định 1/60 để vật lý headless tiến đều. Probe chỉ tồn tại
trong bản sao kiểm thử, không nằm trong scene/prefab hoặc Assets của project thật.
Kết quả và giới hạn: [JOURNEY_INTEGRATION_RESULTS.md](../Development/JOURNEY_INTEGRATION_RESULTS.md).

## Chạy lại

Từ repository root, dùng Unity đã kích hoạt license:

```bash
python3 tools/Integration/audit_journey.py
bash tools/Integration/run-unity.sh /path/to/Editor/Unity /tmp/hus-journey-new-run
```

Thư mục output phải chưa tồn tại. Script sao chép Assets/Packages/ProjectSettings,
compile, chạy toàn bộ Edit Mode và Play Mode, rồi audit/native probe riêng.
Không dùng `-quit` với Test Runner hoặc probe. Timeout hoặc thiếu XML không phải
pass. Kết quả native ghi riêng lỗi gameplay và exception Editor Search đã được
mô tả trong CAMPUS_FOUNDATION.md; mọi lỗi khác đều khiến probe thất bại. Luôn đọc
cả mục KNOWN EDITOR SEARCH ERRORS và log, kể cả khi phần gameplay PASS.

## Chơi thử thủ công

1. Mở `HUS_Escape/` bằng Unity 6000.6.5f1. Mở Bootstrap, bật Console và Play.
   Menu xuất hiện, dùng chuột hoặc Enter ở Start. Nhấn nhanh nhiều lần: chỉ vào
   Floor01 một lần; Hierarchy chỉ có một Core owner và một Journey_Session.
2. WASD di chuyển, chuột xoay camera, E tương tác, F nhặt/đọc, Tab inventory,
   J nhiệm vụ, F5 save/continue, Esc đóng modal. Xác nhận modal khóa di chuyển
   và camera; đóng modal thì điều khiển trở lại bình thường.
3. Làm đủ 17 mục tiêu Floor01: A101, A102, LAB theo HUD/J và các manh mối trong
   phòng. Tham khảo QUEST_V1.md, QUEST_V2_A102.md, QUEST_V3_LAB.md khi cần.
   Ra cửa T3 ở cuối hành lang/vestibule và nhấn E. Chưa đủ nhiệm vụ phải bị từ chối.
4. Ở Campus kiểm tra ba chìa khóa và nhiệm vụ Floor vẫn hoàn thành. Khám phá sân
   trường, đọc sơ đồ và biển hướng; thử vào Office trước/sau ba khám phá.
   Cửa Office tại X=16, Z=53.9; đứng gần Z=51.2..52 rồi E.
5. Trong Office đọc bảng thông báo, tài liệu quầy (F), máy tính (E). Quay cửa
   sảnh, E trở về Campus: xuất hiện gần cửa Office, giữ cả facts Campus/Office
   và inventory, camera theo player mới, đi/xoay được, Time.timeScale=1.
6. Kiểm tra Console, F5 save/continue theo nhu cầu bằng slot thử, cảm giác camera,
   animation, ánh sáng và input trên màn hình thật. Không coi kiểm tra headless
   là nghiệm thu hình ảnh hoặc thao tác giải toàn bộ puzzle.
