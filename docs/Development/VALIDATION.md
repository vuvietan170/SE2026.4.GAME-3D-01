# Validation và build smoke

Runner [validate.py](../../tools/Validation/validate.py) dùng Python standard library, tạo bản sao Unity project ngoài repository; không chỉnh save/project thật. Mỗi bước có timeout, exit code, report và log; fail/timeout/mất report dừng lệnh với exit khác 0. License đang thiếu không được đánh dấu skip/pass.

## Yêu cầu máy

- Unity **6000.6.5f1**, license được kích hoạt hợp lệ bằng Hub/runner của đội; đường dẫn executable truyền bằng `--unity`.
- Python 3.10+; Bash + .NET SDK 8 cho assignment VT-105. Unity Linux có SDK .NET đi kèm, `run.sh` hỗ trợ `DOTNET` override.
- Linux Standalone Build Support cho target `StandaloneLinux64`, hoặc Windows Build Support cho `StandaloneWindows64`. Native player smoke cần chạy trên đúng OS. Target này là smoke-validation target, chưa phải quyết định platform phát hành chính thức.
- Dung lượng cho bản sao Assets, Library, build, cache/package restore; truy cập Package Manager khi chưa có cache. Không đưa Library/build vào Git.

## Lệnh chuẩn

```bash
python3 tools/Validation/audit_repository.py --output /tmp/hus-source-audit.json
python3 tools/Validation/validate.py --unity /path/to/6000.6.5f1/Editor/Unity --output /tmp/hus-validation
```

Trên Windows dùng `py -3`, đường dẫn Unity.exe và output ngoài repository, `--target StandaloneWindows64`. Tất cả step chạy tuần tự để không có hai Editor mở cùng project.

| Step | Thực hiện / bằng chứng |
| --- | --- |
| source-audit (luôn chạy) | `.meta`, GUID duplicate/invalid, script null, build scene, ignore behavior, LFS pointer và kích thước file thực; `source-audit.json`. External package GUID chỉ được kết luận ở native audit. |
| compile | Unity import/compile, `RepositoryValidation.Audit`: resolve GUID, check missing scripts/broken references trong sáu scene và prefab; `native-audit.json`. |
| edit / play | Unity Test Runner, NUnit XML và trạng thái Passed/failed, không chỉ exit 0; `edit.xml`, `play.xml`. Không dùng `-quit` cho Test Runner. |
| gameplay | MVP, Journey integration, PlayerPause, Transaction probes trên scene thật; test scripts/fake engine boundary chỉ được stage vào copy. Result text ghi riêng lỗi Editor Search đã biết. |
| build | `-buildTarget` explicit, BuildPipeline.BuildPlayer với sáu enabled scene, Development build; `build-result.json` có result/error/warning/bytes/player path và fingerprint input. |
| smoke | Chạy executable đã build với instrumentation staging, chờ Bootstrap/menu, Start ba lần, kiểm tra một Core/session và Floor01 mới, return menu rồi Start phiên khác. Kiểm tra exit + `player-smoke.json`. Không đọc hoặc ghi slot save thật. |

Có thể chạy `--steps compile edit play` rồi `--reuse --steps build smoke` trên cùng output. `--reuse` chỉ nhận thư mục có marker của runner và đúng repository; xóa/đồng bộ lại Assets/Packages/ProjectSettings **trong copy**, giữ Library. Bản copy không dành cho soạn game. Fingerprint source + staged smoke code phải khớp build, không nhận executable cũ sau khi source đổi. `validation-summary.json` thuộc lần chạy hiện tại, không cộng dồn các lần cũ.

Smoke instrumentation nằm trong `tools/Validation/BuildSmokeProbe.cs`, được copy vào output, chỉ khởi động khi có flag `--hus-build-smoke`. Không có probe trong `HUS_Escape/Assets` của repository. Build thủ công từ Assets gốc không chứa instrumentation.

Unity khuyến nghị chỉ định build target/profile từ CLI khi dùng custom build script: [Unity 6000.6 — command-line builds](https://docs.unity.com/en-us/engine/6000.6/manual/building-and-publishing/build-command-line).

## CI: cấu hình còn thiếu, chưa bật workflow

Chưa thêm hoặc chạy workflow CI có license. Script local là entry point để đội tích hợp; không thể coi kết quả local như một GitHub Actions run.

Phương án ít phụ thuộc secret mới: runner self-hosted Linux x64 có Unity đúng phiên bản, Linux build module và license đã provision cho user chạy agent. Cần đội cung cấp:

1. Runner/label, đủ disk, quyền chạy Editor và quyền mạng tới Unity Package Manager/licensing theo cách kích hoạt được hỗ trợ.
2. Đường dẫn Editor dưới biến CI `UNITY_EDITOR`; thư mục output riêng theo run trong runner temp, tránh hai job dùng cùng project/Library.
3. Checkout source; chạy audit + `python3 tools/Validation/validate.py --unity "$UNITY_EDITOR" --output "$RUNNER_TEMP/hus-$GITHUB_RUN_ID-$GITHUB_RUN_ATTEMPT"`; upload log/XML/JSON kể cả fail.
4. Nếu dùng cache Library: key gồm OS/Unity version và hash package manifests; runner luôn sync source. Không cache/copy license vào artifact hoặc repository.
5. Đội quyết định branch protection/required checks sau khi workflow đã chạy thật. Không bypass license/build failures bằng `continue-on-error` hoặc tự ghi Passed.

Runner hosted/container cần thêm phương thức licensing được Unity hỗ trợ cho loại license của đội; nếu phương thức đó dùng license/server/credential thì lưu trong secrets của CI, không commit. Chưa có server/license token/secret names do đội xác nhận, chưa cấp runner, chưa chạy cloud CI. Không giả định một biến `UNITY_LICENSE` là đủ cho mọi loại license.

Windows release cần runner Windows + module tương ứng, chạy lại cả build và smoke. Linux batchmode không nghiệm thu visuals, fullscreen/UI scaling, camera feel, gamepad vật lý hay một lần chơi giải toàn bộ puzzle. Checklist vẫn ở [MVP_ESCAPE_LOOP.md](../TechnicalDesign/MVP_ESCAPE_LOOP.md).
