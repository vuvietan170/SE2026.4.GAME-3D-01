# VT-103 — gói nộp và tái kiểm chứng

Nguyễn Hữu Duy — 23001853. Branch: `individual/23001853-vt103-autosave`.

Fork: https://github.com/23001853-wq/SE2026.4.GAME-3D-01

PR target: `vuvietan170/SE2026.4.GAME-3D-01:dev`.

Compare: https://github.com/vuvietan170/SE2026.4.GAME-3D-01/compare/dev...23001853-wq:individual/23001853-vt103-autosave

## Bắt đầu review

- [Báo cáo sửa lỗi và kết quả từng integration test](../../Evidence/VT103/repair-20261114/REPORT.md).
- [Danh sách đầy đủ file trong commit nộp](SUBMISSION_FILES.txt).
- [PDF bài viết](BT3_23001853_VT-103.pdf), [nguồn Markdown](BT3_23001853_VT-103.md), HTML, page fragments và script dựng PDF được giữ nguyên để truy vết.
- Các tài liệu `docs/TechnicalDesign/VT103_*` và PDF là lịch sử các bước RED/GREEN/mutation/performance/integration. Những baseline, đường dẫn evidence hoặc mục PENDING trong đó phản ánh thời điểm viết; kết quả 170 test hiện tại phải đối chiếu XML bên dưới. Các thư mục evidence lịch sử không có trong workspace này không được tự tạo hoặc coi là đã xác minh lại.

## Code cần thiết

- `HUS_Escape/Assets/_Project/Scripts/Utilities/Floor01SaveSystem.cs`: tự gắn autosave và fence quá trình restore.
- `HUS_Escape/Assets/_Project/Scripts/Utilities/Floor01Autosave.cs`: debounce progress, capture an toàn, báo lỗi persistence và hủy pending.
- `HUS_Escape/Assets/_Project/Scripts/Utilities/Autosave/`: writer, contract, hooks, asmdef và toàn bộ EditMode/PlayMode tests, cùng Unity `.meta`.
- `HUS_Escape/Assets/Editor/VT103ManualAcceptanceSandbox.cs`: manual sandbox opt-in, tôn trọng fixture và tắt khi Test Runner chạy.
- `tools/VT103/`: runner hiện tại và các runner/harness lịch sử.

## Hai vòng kiểm thử thực tế

| Vòng | PlayMode | EditMode | Tổng | Fail / Skip | Unity exit |
|---|---:|---:|---:|---|---|
| all-playmode / all-editmode | 45/45 | 125/125 | 170/170 | 0 / 0 | 0 / 0 |
| repeat-play / repeat-edit | 45/45 | 125/125 | 170/170 | 0 / 0 | 0 / 0 |

[Vòng 1 PlayMode](../../Evidence/VT103/repair-20261114/all-playmode/results.xml), [EditMode](../../Evidence/VT103/repair-20261114/all-editmode/results.xml).

[Vòng 2 PlayMode](../../Evidence/VT103/repair-20261114/repeat-play/results.xml), [EditMode](../../Evidence/VT103/repair-20261114/repeat-edit/results.xml).

Mỗi thư mục run có `editor.log`, `command.txt`, `environment.json`, `process.json`, `assertions.json`. Baseline 8/12 và đối chứng 12/12 test nguyên bản cũng được giữ. Snapshot nguồn trước/sau, manifest SHA-256 và fixture JSON được giữ nguyên byte trong `Evidence/VT103/`; `.gitattributes` ngăn Git tự đổi line ending của evidence. Không chỉnh raw output để loại whitespace của Unity. Lỗi licensing trước test không được tính PASS.

## Tái chạy

Yêu cầu Windows, Unity **6000.6.5f1**, license hợp lệ và workspace `D:\Game3D-01\VT103_Work`. Runner hiện tại cố ý giới hạn đúng workspace này. Lần import đầu cần truy cập các package Unity. Không mở cùng project bằng hai Editor.

Trong workspace, dùng bản sao kiểm thử để tránh thay đổi project gốc. Nếu `Temp/VT103Verify` chưa có, chuẩn bị bằng các lệnh sau; nếu đã có thì giữ nguyên bản sao hiện tại và xác minh nó khớp mã nguồn cần kiểm thử trước khi chạy:

```powershell
Set-Location D:\Game3D-01\VT103_Work
if (Test-Path -LiteralPath Temp/VT103Verify) { throw 'Verification copy already exists; do not overwrite it.' }
New-Item -ItemType Directory -Path Temp/VT103Verify | Out-Null
Copy-Item -LiteralPath HUS_Escape/Assets,HUS_Escape/Packages,HUS_Escape/ProjectSettings -Destination Temp/VT103Verify -Recurse
```

Sau đó chọn Phase mới chưa tồn tại để bảo toàn evidence cũ:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/VT103/Run-RepairVerification.ps1 -Phase submission-play -Platform PlayMode -ExpectedTests 45
powershell -NoProfile -ExecutionPolicy Bypass -File tools/VT103/Run-RepairVerification.ps1 -Phase submission-edit -Platform EditMode -ExpectedTests 125
```

Để chỉ chạy 12 integration tests, thêm `-TestFilter HUSEscape.Persistence.Autosave.Tests.Floor01AutosaveIntegrationTests -ExpectedTests 12` vào lệnh PlayMode, dùng Phase khác.

Các runner `Run-Red`, `Run-Green`, `Run-Integration`, `Run-MutationCase`, performance được giữ để review lịch sử. Chúng có guard baseline cũ hoặc phụ thuộc evidence không còn trong workspace; không dùng chúng thay cho `Run-RepairVerification.ps1` để xác minh commit nộp hiện tại. Không vô hiệu hóa guard hoặc ghi đè snapshot lịch sử.

## Phạm vi đóng gói

Không stage ProjectSettings do Unity tự đổi, file `VT103_*_Review.txt`, hai patch thử nghiệm ở root, `clone.log`, `terminated-UnityLockfile`, hoặc bản sao Unity/Library/Temp. Các file đó được giữ nguyên trên máy. `original-editor-test-excerpt.log` là trích đoạn lỗi Test Runner làm bằng chứng nguyên nhân, không phải log cá nhân tổng quát. `helper.diff` và `fixture.diff` là bằng chứng thay đổi trước/sau của lần sửa, không phải patch thử nghiệm.

Không thay đổi code đã PASS hoặc raw evidence khi đóng gói. Các hash trong manifest lịch sử vẫn trỏ tới trạng thái lúc thu thập; đường dẫn tuyệt đối được giữ cho provenance, đường dẫn tương đối ở tài liệu này dùng để review trên GitHub. Việc tạo commit/PR hiện tại được cho phép bởi yêu cầu nộp bài, tiếp nối bước sửa lỗi trước đó.
