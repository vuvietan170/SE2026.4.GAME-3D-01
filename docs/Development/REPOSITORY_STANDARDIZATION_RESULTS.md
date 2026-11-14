# Kết quả chuẩn hóa repository

Nhánh local `chore/repository-standardization` từ `origin/dev` **126ec82** (đã merge MVP PR #12). Đối chiếu `origin/main` tại **c2cb34f**. Không rewrite history hoặc áp dụng LFS migration.

## Thay đổi

- Viết README với Unity 6000.6.5f1, đúng Unity root `HUS_Escape/`, Bootstrap và tuyến MVP, điều khiển, test/build và các giới hạn thật.
- Phân loại tính năng bằng source/scene/harness, không lấy thư mục dự trữ làm bằng chứng hoàn thành; cập nhật PROJECT_STRUCTURE và SAVE_SYSTEMS.
- Xác minh dependency rồi chuyển nguyên byte năm source/legacy test files của SaveSlot ra `assignments/VT105/SaveSystem`; harness .NET ở tools compile trực tiếp source mới. Không có caller SaveSlot trong game, không thêm backend/asmdef giả.
- Cập nhật Harness.csproj và mutation experiment; output mới không ghi đè evidence bài nộp. Legacy NUnit có lỗi cú pháp được giữ như archive và không tính pass.
- Audit sâu tìm 11 GUID reference không resolve trong render template và năm orphan script-null test sub-assets. Loại bỏ block renderer obsolete và chín orphan volume/test blocks, giữ 19 volume components đang được tham chiếu và GUID/.meta của hai asset.
- Thêm `.gitignore` cho `.slnx`, `.vscode` local, Python cache, artifacts; giữ ngoại lệ source Harness.csproj, không ignore metadata/package/ProjectSettings.
- Bổ sung runner disposable-project compile, native audit, Edit/Play Mode, gameplay, build và player smoke; fingerprint ngăn smoke executable cũ khi source đã đổi. Tài liệu CI nêu điều kiện/runner/license còn thiếu; chưa bật workflow.
- Chuẩn bị [mô tả PR dev → main](DEV_TO_MAIN_PR.md), không thực hiện PR.

File chính: [README](../../README.md), [cấu trúc](../Architecture/PROJECT_STRUCTURE.md), [save](../Architecture/SAVE_SYSTEMS.md), [feature status](FEATURE_STATUS.md), [asset/LFS](ASSET_HYGIENE.md), [validation/CI](VALIDATION.md), [assignment](../../assignments/VT105/README.md), `tools/Validation/`, `tools/VT105/`, hai render settings asset và evidence.

## Kết quả thực thi

Chạy Unity **6000.6.5f1** với Linux build module trên bản sao `/tmp/hus-repository-validation`, bằng chính runner mới. Source copy gồm manifests/material hiện có trong workspace; các thay đổi cá nhân đó được giữ riêng, không thuộc phần chuẩn hóa. Không dùng kết quả workspace để tuyên bố candidate commit sạch đã được build.

- Source audit: `.meta`/GUID, ignore rules, build scene order và LFS pointer checks đạt. Không có duplicate/invalid GUID, thiếu `.meta` asset/folder importable hoặc null-script block sau cleanup; `.gitkeep` được loại đúng theo importer.
- Native audit: sáu scene, hai prefab, 285 GUID references resolve, không có missing scripts/broken serialized reference trong phạm vi audit.
- Assignment: 22/22; reverse, repeat, load 150 tasks, config 48h, mutation experiment đều có evidence. Ba auth tests phải fail khi bỏ guard trong temp repository, experiment tổng exit 0. Năm file di chuyển giữ nguyên SHA256/bytes.

| Kiểm tra đã chạy | Kết quả |
| --- | --- |
| Compile + native audit cuối | Passed, exit 0; 6 scene / 2 prefab / 285 GUID references |
| Edit Mode / Play Mode | 110/110 / 33/33, failed/skipped 0 |
| MVP / Journey / player-pause / transactions | 16/16 (163 assertions) / 49 checks / 20/20 / 34/34 |
| VT-105 harness và experiments | 22/22; reverse/repeat/load/config/mutation đạt kỳ vọng |
| Linux development build | Succeeded, 0 errors, 2 warnings, 313,677,439 bytes (~299.15 MiB) |
| Linux executable smoke | Passed: Bootstrap → menu → Floor01 → menu → phiên mới, 1 Core / 1 Session |
| Ignore/GUID/metadata, script syntax và diff checks | Passed |

[Machine summary](RepositoryEvidence/summary.json), [build report](RepositoryEvidence/build-result.json), [player smoke](RepositoryEvidence/player-smoke.json), [native audit](RepositoryEvidence/native-audit.json), [source audit](RepositoryEvidence/source-audit.json), [assignment](RepositoryEvidence/assignment-green.txt), [experiments](RepositoryEvidence/assignment-experiments.json).

Full runner lần đầu đạt toàn bộ suite và build, nhưng smoke fail vì kiểm tra trước end-of-frame deferred Destroy. Đã sửa **chỉ probe được stage vào bản sao**, không đổi Journey/game runtime; chạy lại compile/build/smoke đạt. Giữ cả [summary lần đầu](RepositoryEvidence/validation-first-build.json), [smoke fail](RepositoryEvidence/player-smoke-first.json) và [summary cuối](RepositoryEvidence/validation-summary.json). Không biến lần fail thành pass hoặc báo các suite đã chạy lại khi không cần thiết.

Executable ở `/tmp/hus-repository-validation/BuildSmoke/HUSEscape.x86_64`; log/XML/build không được đưa vào Git. Chỉ evidence gọn ở docs. Development player này có smoke instrumentation, không được coi là bản release nghiệm thu hình ảnh.

## Bằng chứng

[Audit trước cleanup](RepositoryEvidence/native-before.json) · [render cleanup](RepositoryEvidence/render-cleanup.json) · [assignment move](RepositoryEvidence/assignment-move.json). Log đầy đủ, XML và build output ở project validation tạm; evidence gọn được lưu trong RepositoryEvidence.

## Còn cần thực hiện

- Chạy toàn bộ trên candidate dev sạch sau khi review phần chuẩn hóa và các thay đổi cá nhân cần giữ/loại khỏi candidate; cập nhật bằng chứng trước main.
- Nghiệm thu thủ công trên Editor/player có đồ họa: tự giải đủ 17 puzzle, toàn tuyến Office/cổng, caught/retry/menu, camera/animation/UI scaling/font/fullscreen và gamepad vật lý. Batchmode/fixture không thay thế việc này.
- Chọn platform phát hành; nếu Windows, cài module và chạy cả build lẫn executable smoke trên Windows. Kết quả Linux không phải nghiệm thu Windows.
- Cấu hình CI runner, đường dẫn Unity, license/build support và artifact upload trước khi bật required checks; chưa có cloud CI run được xác nhận.
- Nếu chọn LFS cho chín FBX lớn, thống nhất quota/quy trình ở thay đổi riêng; không rewrite history. Chưa áp dụng LFS trong đợt này.

Editor Search/IndexationOnStartup và Animator warning đã tồn tại được ghi riêng. Không tuyên bố mọi Console của project sạch tuyệt đối. Thay đổi material/package và tài liệu cá nhân có sẵn không bị xóa; file IDE local chỉ được ignore, vẫn còn trên đĩa. Các thay đổi cá nhân có sẵn không thuộc commit chuẩn hóa.
