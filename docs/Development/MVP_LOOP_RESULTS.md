# Kết quả vòng chơi MVP

Base `origin/dev` = `b9eba8e` (PR #11), nhánh `feat/mvp-escape-loop`.
Đã đọc QuestManager, JourneySession, PatrolGuard, composition scene, tài liệu Office/stealth/save/input và menu/Core trước khi sửa. Không tìm thấy AGENTS.md áp dụng trong checkout/ancestor. Giữ nguyên thay đổi cá nhân có sẵn ở material, package manifests và các file chưa track.

## Hành vi đã bổ sung — thiết kế đề xuất

- Floor01 → Campus → TrainingOffice → Campus/Cổng chính. Cổng primitive ở phía Bắc Campus; E chỉ thắng sau LAB, ba Campus facts và ba Office facts. Không thêm scene hoặc sửa Build Settings.
- Bảo vệ người có hậu quả bắt khi Alert, đủ gần và có tầm nhìn; chó giữ báo động. Màn bị bắt khóa gameplay và cho retry/menu.
- Retry tải lại khu vực qua SceneLoader, giữ tiến trình đang có và vật phẩm, về spawn và reset AI. Chơi lại từ màn thắng dùng New Game để về Floor01 với dữ liệu phiên mới.
- Màn thắng, retry và về menu dùng UI của Session đang có. Menu chỉ dọn Session sau load thành công; Start dùng lại Core và tạo Session mới. New Game/menu không xóa hoặc ghi đè save trên đĩa.

Tất cả lựa chọn mới, tham số và checklist được ghi là **đề xuất** trong [MVP_ESCAPE_LOOP.md](../TechnicalDesign/MVP_ESCAPE_LOOP.md).

## Kết quả thực thi

Unity **6000.6.5f1**, batchmode/nographics, project copy `/tmp/hus-integration-validation`; storage probe riêng trong project tạm, không tác động save thật. Các kết quả sau đã thực thi, không suy đoán:

| Kiểm tra | Kết quả |
| --- | --- |
| Unity compile | exit 0, không có lỗi/cảnh báo C# compiler |
| Edit Mode | Passed 110/110, failed/skipped 0 |
| Play Mode | Passed 33/33, failed/skipped 0 |
| MVP runtime | Passed 16/16, 163 assertions (gồm setup checks) |
| Audit native toàn bộ Build Settings | Passed 6/6 scene |
| Native Bootstrap/menu/Journey cũ | Passed 49 checks |
| Player/pause regression | Passed 20/20 |
| Save/Journey transaction regression | Passed 34/34 |
| Audit GUID, scene/prefab controller use, UI map, Build Settings | Passed |
| git diff --check; bash -n runner | Passed |

[MVP evidence](MvpLoopEvidence/green.txt), [scene audit](MvpLoopEvidence/scenes.txt), [Journey](MvpLoopEvidence/journey.txt), [player/pause](MvpLoopEvidence/player-pause.txt), [transactions](MvpLoopEvidence/transactions.txt), [machine summary và log paths](MvpLoopEvidence/execution.json).

Các ca MVP đi qua guard Update thật đến Alert/Caught, E/F tại các điểm Office, E tại cổng, Enter retry, F5 modal, return menu và nhấn Start nhiều lần. Có kiểm tra cổng thiếu progress/đứng xa, không bắt xuyên vật cản/từ xa/bởi chó, kết quả chỉ ghi một lần, retry giữ fact/inventory/spawn/AI, New Game/menu giữ nguyên byte ba slot thử, Session mới không kế thừa quest cũ, đúng một Core/Session.

Native route từ Bootstrap sử dụng checkpoint Floor01 hợp lệ và Campus facts làm fixture để cô lập phần MVP mới; có dịch chuyển actor ở probe để kích hoạt từng điểm thật. Không mô tả probe này như một lần người chơi tự đi bộ và giải cả 17 puzzle. Chơi xuyên suốt bằng tay vẫn nằm trong checklist.

Fault injection chỉ thay engine boundary trong bản sao Core. Các ca gồm rejection/failure retry/menu khi source còn, modal đang mở, source đã unload và retry menu từ recovery. Khi phát hiện menu thiếu view sau load thành công, phiên nguồn phải trở lại Playing và có input; đã tái hiện thất bại **15/16**, sửa rồi chạy đạt **16/16**. [Evidence trước sửa](MvpLoopEvidence/menu-invalid-red.txt).

### Console và phần chưa chạy

Không có runtime error bất ngờ trong các probe. Log lỗi do failure injection có chủ đích được nhận diện chính xác; probe duplicate controller cũ cũng có log từ chối có chủ đích. Exception Editor Search/IndexationOnStartup và warning Animator đã tồn tại được ghi riêng, không tuyên bố Console toàn project sạch tuyệt đối.

Chưa nghiệm thu bằng cửa sổ đồ họa: bố cục màn thắng/bị bắt, font/hình ảnh biển, collision/cảm giác camera, gamepad vật lý và một lần tự giải toàn bộ puzzle bằng tay. Native audit đã xác nhận font/material của hai biển, primitive collider, scene links và không có missing scripts.

## File thay đổi

Các đường dẫn Assets tính từ `HUS_Escape/Assets/`:

- `_Project/Scripts/Utilities/JourneySession.cs`: Outcome, capture subscription, retry/menu transaction, terminal UI và cleanup phiên.
- `_Project/Scripts/AI/PatrolGuard.cs`: điều kiện và event bắt, giữ thuật toán tuần tra/nghe/truy đuổi hiện có.
- `_Project/Scripts/Interaction/MainGateExit.cs` + `.meta`: tương tác E và xác thực cổng của scene hiện tại.
- `_Project/Scripts/Puzzle/QuestManager.cs`: mục tiêu cuối chỉ hướng cổng; không thêm quest/save facts.
- `_Project/Scripts/Puzzle/QuestPuzzleUI.cs`: nút về menu từ F5.
- `_Project/Scripts/UI/MainMenuFlow.cs`: cấu hình trên Core hiện có khi mở gameplay trực tiếp trong Editor.
- `_Project/Scenes/Campus_Prototype.unity`: thêm một root cổng/biển primitive. Giữ nguyên **820 block gốc**, chỉ cập nhật SceneRoots và thêm **81 block** mới; không renumber asset cũ.
- `_Project/Scenes/TrainingOffice_Prototype.unity`: một nội dung máy tính hướng dẫn về cổng.
- `tools/MvpLoop/`: setup authoring, native scene audit, runtime probe và runner dùng project tạm.
- `docs/TechnicalDesign/MVP_ESCAPE_LOOP.md`; link cập nhật trong `CAMPUS_STEALTH_V2.md`, `TRAINING_OFFICE_FOUNDATION.md`; báo cáo và evidence này.

[Asset preservation evidence](MvpLoopEvidence/asset-preservation.json): giữ nguyên `.meta` của Session/Quest/Guard; Build Settings nguyên byte; các controller/camera/inventory và reference có sẵn không bị thay component. Save V1 không đổi.

Các thay đổi cá nhân có sẵn không được đưa vào commit MVP. Chạy lại toàn bộ bằng:

```bash
bash tools/MvpLoop/run-unity.sh /path/to/Unity /tmp/new-mvp-validation
```
