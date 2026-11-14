# Trạng thái tính năng theo bằng chứng

Base kiểm tra `dev` `126ec82` (đã gồm PR #12). “Có mã/scene” và “MVP chạy được trong probe” không đồng nghĩa chất lượng release đã nghiệm thu.

| Nhóm | Bằng chứng có thật | Phạm vi và giới hạn |
| --- | --- | --- |
| Core/menu/scene | GameBootstrap, GameManager, SceneLoader, MainMenuFlow/View; Bootstrap và MainMenu trong Build Settings | Một owner, Start Floor01, transition và failure/recovery. SampleScene vẫn phục vụ test. |
| Player/camera/pause | PlayerController implementation chung, PlayerController1 compatibility, ThirdPersonCameraFollow, GameplayInputGate/Coordinator | Move theo camera, sprint/jump/crouch/animation; modal owner và đóng cùng frame. Crouch không đổi collider. Gamepad bindings có nhưng cần thiết bị thật. |
| Floor01 | Scene Floor01_Prototype, QuestManager 17 definitions và CodeLock/Sequence/Lab/Clue, door/pickup/IK | Có puzzle/quest loop; không suy ra đã có nhiều tầng. Thang máy chỉ thông báo chưa hoạt động. |
| Campus/Office | Hai scene thật, CampusDiscovery, OfficeDiscovery, TrainingOfficeEntrance | Ba Campus facts và ba Office facts; Office là discovery prototype, không mô phỏng thủ tục hành chính đầy đủ. |
| Stealth/MVP end | PatrolGuard/CampusStealthHUD, MainGateExit trong Campus, JourneyOutcome | Patrol/sight/hearing/alarm/local steering, bắt/retry, cổng/thắng/menu. Primitive và IMGUI; chưa NavMesh/pathfinding phức tạp hoặc balancing final. |
| Save game | Floor01SaveSystem, SaveJson, JourneySession, UI F5 | Ba file V1 local, manual overwrite/Continue/New Game; rollback/recovery. Không cloud/account hay slot-service integration. |
| SaveSlot assignment | Source riêng + tools/VT105 harness | .NET exercise có auth/retention; không được tính là runtime save feature của game. |
| Art/UI/Audio/Resources/Prefabs dự trữ | Nhiều thư mục chỉ có `.gitkeep`/`.meta` | Không tính là model library, audio engine, item architecture, HUD assets hay prefab pipeline đã hoàn thành. `Assets/Player` thật chứa FBX/Animator, không suy rộng ra toàn bộ art final. |
| Scene Zone01..04 dự trữ | `Scenes/Zone*/.gitkeep` | Gameplay dùng các `*_Prototype.unity`; Zone04_MainGate không có scene riêng. Cổng hiện ở Campus. |
| Chưa triển khai | Không có runtime tương ứng trong Assets | Multiplayer/lobby/account/cloud save, vùng/tầng tiếp theo, elevator travel, âm nhạc/voice/SFX hoàn chỉnh, narrative/art/UI final, release platform đã nghiệm thu. |

Cần chơi tay: giải đủ 17 puzzle, tránh bảo vệ tự nhiên, font/visuals/UI scaling/camera, gamepad vật lý và cân bằng. Đọc [checklist MVP](../TechnicalDesign/MVP_ESCAPE_LOOP.md); không lấy một thư mục rỗng hay chỉ một test Core làm bằng chứng cho gameplay end-to-end.

Các evidence lịch sử theo từng PR vẫn giữ trong Development; kết quả chạy lại của đợt chuẩn hóa được tách riêng ở [REPOSITORY_STANDARDIZATION_RESULTS.md](REPOSITORY_STANDARDIZATION_RESULTS.md).
