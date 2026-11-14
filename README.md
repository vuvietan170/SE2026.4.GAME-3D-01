# HUS Escape — SE2026.4.GAME-3D-01

Game Unity 3D: giải nhiệm vụ Floor01, khám phá Campus và Phòng Đào tạo, tránh bảo vệ rồi ra Cổng chính. Vòng chơi hiện tại là **MVP/prototype**, chưa phải bản phát hành hoàn chỉnh.

## Mở và chơi

- Unity Editor **6000.6.5f1** (URP); phiên bản gốc ở [ProjectVersion.txt](HUS_Escape/ProjectSettings/ProjectVersion.txt).
- Unity Hub → Projects → Add/Open project from disk → chọn **thư mục `HUS_Escape/`**, nơi có `Assets`, `Packages`, `ProjectSettings`. Không chọn repository root hoặc `assignments/`.
- Đợi import/compile. Mở `Assets/_Project/Scenes/Bootstrap/Bootstrap.unity`, nhấn Play rồi Start. Nếu Hierarchy chỉ có `Untitled`, hãy double-click scene Bootstrap trong Project trước.
- Tuyến: **Bootstrap → MainMenu → Floor01 → Campus → TrainingOffice → Campus/Cổng chính → chiến thắng**. Cổng là primitive ở phía Bắc Campus, không phải scene thứ tư. SampleScene giữ cho test, không phải điểm Start của menu.

| Điều khiển | Thao tác |
| --- | --- |
| WASD / chuột | Di chuyển theo camera / xoay camera |
| Shift / Space | Chạy / nhảy |
| C hoặc Ctrl | Crouch: đổi tư thế và tốc độ, collider giữ nguyên |
| E / F | Tương tác / nhặt, đọc tài liệu |
| Tab / J / F5 | Inventory / nhật ký / tiến trình Save–Continue–New Game–Menu |
| Esc | Đóng modal; tại màn kết thúc trở về menu |
| Enter | Xác nhận puzzle; tại màn bị bắt thử lại, tại màn thắng chơi lại từ đầu |
| 0–9, numpad; A/B/C; Backspace/Delete | Nhập mã, thứ tự puzzle, sửa/xóa |

Sau 17 mục tiêu Floor01, E tại lối ra T3. Campus có ba khám phá; Office có bảng thông báo (E), tài liệu quầy (F), máy tính (E). Hoàn tất Office, quay về Campus, tới cổng khoảng `(0, 1.08, 59.3)` và E. Bị bắt có retry khu vực (giữ tiến trình, về spawn); Chơi lại/New Game đặt lại phiên. **Không thao tác nào tự xóa save trên đĩa.** Chi tiết: [checklist chơi xuyên suốt](docs/TechnicalDesign/MVP_ESCAPE_LOOP.md).

## Trạng thái tính năng

| Trạng thái | Có gì thực tế |
| --- | --- |
| Đã triển khai trong MVP | Bootstrap/menu/SceneLoader; một Core và JourneySession; camera-relative movement, sprint/jump/crouch/animation; inventory và khóa input/pause theo modal; 17 mục tiêu Floor01 + ba Campus + ba Office; manual save/Continue/New Game V1; cổng, thắng, bị bắt, retry/menu và phục hồi khi load lỗi. |
| Prototype / cần nghiệm thu | Geometry và cổng primitive, Office dạng discovery; AI tuần tra/nhìn/nghe/local steering, chó báo động; UI IMGUI, door IK, camera collision. Gamepad có bindings nhưng chưa nghiệm thu thiết bị thật; chất lượng hình ảnh, cân bằng, cảm giác chơi và một lần tự giải toàn bộ puzzle vẫn cần chơi tay. |
| Chưa triển khai | Scene MainGate riêng, tuyến tầng/khu vực mới, thang máy hoạt động; NavMesh/pathfinding hoàn chỉnh; multiplayer/account/cloud save; hệ thống âm nhạc/voice/SFX hoàn chỉnh, art/UI final và pipeline phát hành đã nghiệm thu. |

Các thư mục `Art`, `Audio`, `Prefabs`, `UI`, `Resources`, `Scripts/Audio`, `Scripts/Items`, `Scenes/Zone*` có thể chỉ là chỗ dành sẵn (`.gitkeep`/`.meta`); **sự tồn tại của thư mục không chứng minh tính năng đã hoàn thành**. Danh mục theo bằng chứng: [FEATURE_STATUS.md](docs/Development/FEATURE_STATUS.md).

## Test và build có thể chạy lại

Cần Python 3, Unity đúng phiên bản đã kích hoạt license và build support của target. Chạy từ repository root; output phải **ngoài repository**. Runner tạo bản sao project, không chạy probe trên project/save thật:

```bash
python3 tools/Validation/validate.py --unity /path/to/6000.6.5f1/Editor/Unity --output /tmp/hus-validation
```

Mặc định: audit source → compile/native audit → Edit Mode → Play Mode → gameplay probes → Linux/Windows development build → chạy player smoke **Bootstrap → menu → Start → Floor01 → menu → phiên mới**. Trên Windows:

```powershell
py -3 tools/Validation/validate.py --unity "C:\Program Files\Unity\Hub\Editor\6000.6.5f1\Editor\Unity.exe" --output "C:\Temp\hus-validation" --target StandaloneWindows64
```

Chạy từng phần, hoặc dùng lại đúng project tạm do runner tạo:

```bash
python3 tools/Validation/validate.py --unity /path/to/Unity --output /tmp/hus-checks --steps compile edit play
python3 tools/Validation/validate.py --unity /path/to/Unity --output /tmp/hus-checks --reuse --steps build smoke
python3 tools/Validation/audit_repository.py --output /tmp/hus-assets.json
```

Log, NUnit XML, native audit, build report và `validation-summary.json` nằm trong output. `--reuse` đồng bộ lại ba thư mục source của **bản sao** và giữ Library; không dùng output để soạn game. Timeout/thiếu report/lỗi license không phải test pass. Cross-build Windows trên Linux không được tính là đã chạy player Windows. Chi tiết và cấu hình CI còn thiếu: [VALIDATION.md](docs/Development/VALIDATION.md).

Trong Editor: Window → General → Test Runner → EditMode/PlayMode → Run All. Build thủ công: File → Build Profiles, cài support cho target, kiểm tra sáu scene enabled theo thứ tự trong [EditorBuildSettings.asset](HUS_Escape/ProjectSettings/EditorBuildSettings.asset), Bootstrap đứng đầu, rồi Build. Runner smoke có instrumentation chỉ nằm trong bản sao test; build phát hành bằng Assets game gốc không chứa probe này.

## Save và cấu trúc

- Game: `Floor01SaveSystem` sở hữu `floor01-v1.json`; `JourneySession` giữ dữ liệu phiên và `campus-v1.json`/`trainingoffice-v1.json`. File tại `Application.persistentDataPath`, lưu thủ công bằng F5, ghi đè cần xác nhận. [Quan hệ và giới hạn save](docs/Architecture/SAVE_SYSTEMS.md).
- `assignments/VT105/`: SaveSlotService bài tập .NET độc lập, không được Unity import, không phải backend save của game. Chạy `bash tools/VT105/run.sh`; [hướng dẫn assignment](assignments/VT105/README.md).
- `tools/`: authoring, harness và validation; `docs/`: thiết kế, evidence và checklist. [Cấu trúc repository](docs/Architecture/PROJECT_STRUCTURE.md).

[Kết quả chuẩn hóa thực tế](docs/Development/REPOSITORY_STANDARDIZATION_RESULTS.md) · [Audit asset và đề xuất LFS](docs/Development/ASSET_HYGIENE.md) · [Mô tả PR dev → main đã chuẩn bị](docs/Development/DEV_TO_MAIN_PR.md).
