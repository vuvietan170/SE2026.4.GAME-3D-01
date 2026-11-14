# Kết quả kiểm tra Player / Pause

Nhánh `fix/player-input-pause-consistency`, base `origin/dev` = `289de8b` (đã gồm PR #9 và #10). Các thay đổi cá nhân có sẵn ở material, package manifests và file chưa track được giữ lại; không thuộc phần sửa này.

## Tái hiện trước khi sửa

Chạy Unity thật trên bản sao project, trước khi sửa implementation:

- **0/7**: inventory không pause Core/gameplay; Crouch C bị bỏ qua; release một owner mở khóa owner khác; Gate pause không đồng bộ GameManager; bật lại camera làm Look hoạt động trong pause; disable gate để UI mở; có thể gắn cả hai controller trên một CharacterController. Evidence: [red.txt](PlayerPauseEvidence/red.txt).
- Bộ mở rộng **16/17**: một Esc đóng cả hai modal. Evidence: [nested-red.txt](PlayerPauseEvidence/nested-red.txt). Sau đó thêm thứ tự modal và consume lệnh đóng trong frame.
- Hồi quy transaction **33/34** tìm thấy đóng modal giữa Loading làm clock chạy lại quá sớm. Evidence: [travel-red.txt](PlayerPauseEvidence/travel-red.txt). Đã dùng lease Journey riêng đến hết chuyến đi; startup hold vẫn chỉ chặn input.

## Kết quả thực thi

Unity **6000.6.5f1**, batchmode/nographics, project dùng một lần `/tmp/hus-integration-validation` (Assets/Packages/ProjectSettings copy từ workspace, Library riêng). Không chạy probe trên project/save thật. Runtime fixtures dùng thiết bị input ảo, scene và UI thật.

| Kiểm tra | Kết quả |
| --- | --- |
| Unity compile source cuối | exit 0, không có compiler error/warning C# |
| Edit Mode hiện có | Passed 110/110, failed 0, skipped 0 |
| Play Mode hiện có | Passed 33/33, failed 0, skipped 0 |
| Player/pause runtime probe | Passed 20/20 |
| Serialization native bốn gameplay scene | Passed 4/4 |
| Bootstrap/menu và native Journey | Passed 49 checks |
| Journey/save transaction regression | Passed 34/34 |
| Audit GUID, scene/prefab, UI map, Build Settings | Passed |
| git diff --check | Passed |

[Player probe](PlayerPauseEvidence/green.txt), [serialization](PlayerPauseEvidence/serialization-green.txt), [native Journey](PlayerPauseEvidence/journey-green.txt), [transactions](PlayerPauseEvidence/transactions-green.txt), [machine summary](PlayerPauseEvidence/execution.json).

20 ca player/pause gồm: Tab/F5/J thật, C và Ctrl, owner lồng nhau, đồng bộ Paused/Playing, camera enable trong pause, disable UI/gate rồi enable, duplicate controller bị từ chối, giữ gamepad Cancel không crouch, giữ pause bên ngoài, snapshot bỏ modal đã đóng, unload khôi phục timeScale .5 và cursor, movement theo camera, sprint/animation, jump/capsule crouch không đổi, isolation action giữa actor, actor/camera bất động khi pause, native Floor→Campus và New Game về Floor khi F5 đang mở.

Transaction regression giữ các nhánh rejection/failure/Attach lỗi, source còn/mất, recovery, modal mở hoặc đóng giữa Loading, restore rollback và Continue lặp. Native Journey kiểm tra một Core/session, inventory/facts/spawn/camera/input trên Floor→Campus→Office→Campus.

### Console và giới hạn

Không có lỗi runtime bất ngờ trong các probe. Test cố tình thêm controller trùng có log từ chối của Unity và được nhận diện riêng. Một số lần khởi động Editor có exception `UnityEditor.Search.SearchDatabase / IndexationOnStartup` đã quan sát trên dev trước sửa; lưu riêng trong evidence, không quy thành runtime pass rồi bỏ qua. Animator có warning chuyển trạng thái đã tồn tại. Không tuyên bố Console toàn project sạch tuyệt đối.

Chưa chơi thử với cửa sổ đồ họa, gamepad vật lý, hoặc đánh giá hình ảnh/animation/camera collision bằng mắt. Checklist nằm trong [PLAYER_INPUT_PAUSE.md](../TechnicalDesign/PLAYER_INPUT_PAUSE.md#checklist-chơi-thử-có-hình-ảnh).

## File sửa

Các đường dẫn bên dưới tính từ `HUS_Escape/Assets/`:

- `_Project/Scripts/Player/PlayerController.cs`, `PlayerController1.cs`: implementation chung và lớp tương thích giữ GUID/reference.
- `_Project/Scripts/Player/GameplayInputGate.cs`, `GameplayPauseCoordinator.cs` + `.meta`, `HUSEscape.Player.asmdef`: action theo actor, lease owner, coordinator trên Core hiện có.
- `_Project/Scripts/Player/ThirdPersonCameraFollow.cs`: đọc Look qua gate, không tự enable action, đứng yên khi bị chặn.
- `_Project/Scripts/Inventory/InventoryUI.cs`, `_Project/Scripts/Puzzle/QuestPuzzleUI.cs`: lease riêng, lifecycle cleanup, Input Actions, modal trên cùng.
- `_Project/Scripts/Utilities/JourneySceneEntry.cs`, `JourneySession.cs`: phân biệt startup/travel/modal và bind Core rõ ràng.
- `InputSystem_Actions.inputactions`: thêm GameplayUI, Pickup, Ctrl alias; giữ UI map và Player bindings/ID cũ.
- `_Project/Scripts/Interaction/InteractionSelection.cs`, `DoorInteraction.cs`, `PickupItem.cs`, `BuildingExit.cs`, `ElevatorUnavailable.cs`, `TrainingOfficeEntrance.cs`; `_Project/Scripts/Puzzle/CampusDiscovery.cs`, `OfficeDiscovery.cs`: chuyển E/F sang action qua gate.
- `tools/PlayerPause/`: runner, native serialization audit, runtime probe và source audit.
- `docs/TechnicalDesign/PLAYER_INPUT_PAUSE.md`, cập nhật link trong `PLAYER_FOUNDATION.md`; báo cáo và evidence hiện tại.

Không đổi scene/prefab, Build Settings hay `.meta` của các script hiện có. Save V1 không thay đổi. Các thay đổi cá nhân có sẵn không được đưa vào commit player/pause.

Chạy lại toàn bộ bằng `bash tools/PlayerPause/run-unity.sh /path/to/Unity /tmp/new-player-validation`.
