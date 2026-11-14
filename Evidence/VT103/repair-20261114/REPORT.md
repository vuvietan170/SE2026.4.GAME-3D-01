# VT-103: sửa bốn lỗi integration — 14/11/2026

Kết quả thực chạy cuối: **170/170 PASS**, 0 failed, 0 skipped. Unity **6000.6.5f1**. Giữ toàn bộ assertion cũ. Không cần sửa production code: nguyên nhân là helper Editor can thiệp storage của test.

## Nguyên nhân được xác nhận trước khi sửa

Đã đọc toàn bộ `Floor01Autosave.cs`, `DraftStore.cs`, `Floor01SaveSystem.cs`, `Floor01AutosaveIntegrationTests.cs`, sau đó truy tiếp `QuestManager`, `PlayerInventory` và `VT103ManualAcceptanceSandbox`.

`VT103ManualAcceptanceSandbox` có `[InitializeOnLoad]`, tự đăng ký `playModeStateChanged` cho mọi lần vào Play Mode, kể cả Test Runner. `Tick()` ghi đè `StorageDirectoryOverride` và gọi lại `Floor01Autosave.Bind()` khi đường dẫn không khớp thư mục manual acceptance. Mọi fixture GUID bị chuyển về cùng một thư mục của cả phiên Play Mode. `Bind()` còn gọi `CancelWork()`, nên helper cũng có thể hủy pending của fixture.

Log gốc được giữ tại [original-editor-test-excerpt.log](original-editor-test-excerpt.log). Các dòng `Floor01 autosave rebound to` cho thấy nhiều scene/test liên tiếp dùng cùng một đường dẫn, cùng log lỗi deserialize sau I10.

| Test | Nguyên nhân lỗi thực tế |
|---|---|
| I05_PersistenceFailurePreservesTheActiveSession | Directory gây lỗi được tạo trong `directory` của fixture; helper chuyển draft sang thư mục khác. Autosave ghi thành công, `LastError` rỗng. Thao tác gây lỗi nhắm sai file sau khi bị helper đổi đường dẫn. |
| I07_NewGameCancelsPendingProgressWithoutAutosavingRestoredState | Draft ở thư mục chung đã tồn tại từ các test trước. Assertion `File.Exists(DraftPath) == false` thất bại dù New Game không tạo commit mới. |
| I11_ExistingHeadSeedsRevisionWithoutRestoringItsPayload | I10 để lại `not a valid draft` trên đường dẫn chung. Seed revision 100 gặp file hỏng và fault ở `ReadHead()`. `DraftStore` từ chối ghi đè là hành vi đúng. |
| I12_ManualContinueRestoresOnlyManualSlotAndDoesNotAutosave | Helper chuyển manual save khỏi thư mục fixture; fixture đọc sentinel thay vì checkpoint vừa ghi. Đồng thời draft hỏng từ I10 làm autosave không đạt commit 1. Trong lần tái hiện, assertion commit 1 thất bại trước bước Continue. |

Đối chứng: `baseline-licensed` tái hiện đúng **8 PASS / 4 FAIL**, đúng bốn tên trên. Sau đó chỉ cập nhật helper, giữ nguyên cả production lẫn 12 test nguyên bản: `fixed-original-tests` đạt **12/12 PASS**. Vì vậy không sửa luồng production đang xử lý đúng các tình huống này.

## File thay đổi trong lần xử lý này

- `HUS_Escape/Assets/Editor/VT103ManualAcceptanceSandbox.cs`: mặc định tắt, opt-in qua `Tools/VT103/Toggle manual acceptance sandbox`; batch mode không kích hoạt; callback Test Runner thu hồi opt-in và gỡ subscription manual; tôn trọng storage override có chủ sở hữu; đường dẫn manual acceptance nằm trong workspace.
- `HUS_Escape/Assets/_Project/Scripts/Utilities/Autosave/Tests/PlayMode/Floor01AutosaveIntegrationTests.cs`: giữ toàn bộ assertion cũ, bổ sung assertion đường dẫn sau setup và xuyên suốt coroutine chờ; so sánh byte thực cho cả ba manual slot; kiểm tra pending còn sau lỗi ghi, staging được dọn, pending có thật trước New Game, và manual slots không đổi sau reset/corrupt/Continue.
- `HUS_Escape/Assets/_Project/Scripts/Utilities/Autosave/Tests/Editor/ManualAcceptanceIsolationTests.cs` và `.meta`: bốn regression tests cho quyền sở hữu storage, path của chính helper, callback Test Runner và chế độ mặc định tắt.
- `tools/VT103/Run-RepairVerification.ps1`: runner mới dùng bản sao trong workspace, lưu lệnh/env/log/XML/process/assertions; từ chối ghi đè thư mục evidence đã tồn tại; lỗi, skip, timeout, thiếu XML hoặc thiếu test đều không được tính PASS.

`Floor01Autosave.cs`, `DraftStore.cs`, `Floor01SaveSystem.cs` **byte-identical với snapshot trước xử lý**. Các thay đổi có sẵn trong workspace trước nhiệm vụ này được giữ nguyên.

## Kết quả từng PlayMode integration test

| Test | Baseline | Cuối cùng |
|---|---|---|
| I01_NewQuestProgressCreatesValidatedFloorDraft | PASS | PASS |
| I02_RepeatedFactAndIdleFramesDoNotCommitAgain | PASS | PASS |
| I03_ReversedCapturedRequestsCannotRegressDiskRevision | PASS | PASS |
| I04_AutosavePreservesAllManualSlotsByteForByte | PASS | PASS |
| I05_PersistenceFailurePreservesTheActiveSession | FAIL | PASS |
| I06_FloorSceneAutomaticallyComposesOneAutosaveOwner | PASS | PASS |
| I07_NewGameCancelsPendingProgressWithoutAutosavingRestoredState | FAIL | PASS |
| I08_UnsafeAirborneCaptureDefersThenRetriesAfterGrounding | PASS | PASS |
| I09_ReenableDoesNotDuplicateEventSubscriptions | PASS | PASS |
| I10_CorruptDraftIsPreservedAndDoesNotLoadIntoGameplay | PASS | PASS |
| I11_ExistingHeadSeedsRevisionWithoutRestoringItsPayload | FAIL | PASS |
| I12_ManualContinueRestoresOnlyManualSlotAndDoesNotAutosave | FAIL | PASS |

## Toàn bộ test và bằng chứng chạy

| Run | Thực thi | PASS | FAIL | Skip | Unity exit | Thời gian |
|---|---:|---:|---:|---:|---:|---:|
| baseline | 0 | — | — | — | -1 | 105.37 s |
| baseline-licensed | 12 | 8 | 4 | 0 | 2 | 104.14 s |
| fixed-original-tests | 12 | 12 | 0 | 0 | 0 | 24.57 s |
| all-playmode | 45 | 45 | 0 | 0 | 0 | 31.84 s |
| all-editmode | 125 | 125 | 0 | 0 | 0 | 12.26 s |

`baseline` bị kẹt IPC Unity Licensing Client trước test, đã dừng riêng tiến trình do runner tạo. Không có XML và không được tính PASS. Lần chạy sau có quyền kết nối licensing mới thực thi được test. Cả bốn test thất bại được tái hiện và sửa.

| Assembly cuối cùng | PASS / Total |
|---|---:|
| HUSEscape.Core.PlayModeTests | 20 / 20 |
| HUSEscape.UI.PlayModeTests | 13 / 13 |
| VT103.Autosave.PlayModeTests | 12 / 12 |
| HUSEscape.Core.EditModeTests | 98 / 98 |
| HUSEscape.UI.EditModeTests | 12 / 12 |
| VT103.Autosave.EditModeTests | 15 / 15 |

15 EditMode VT103 gồm T01–T08 (8), S01–S03 (3), và bốn test `ManualAcceptanceIsolationTests`. 143 test Core/UI cũ đều PASS. Phạm vi regression là toàn bộ test được Unity Test Runner khám phá trong project `HUS_Escape`; không tuyên bố đã chạy các native route/visual probe riêng trong `tools/` hoặc mã ngoài Unity project.

- [PlayMode XML](all-playmode/results.xml), [log](all-playmode/editor.log), [process](all-playmode/process.json), [assertions](all-playmode/assertions.json).
- [EditMode XML](all-editmode/results.xml), [log](all-editmode/editor.log), [process](all-editmode/process.json), [assertions](all-editmode/assertions.json).
- [Baseline XML](baseline-licensed/results.xml), [baseline log](baseline-licensed/editor.log).
- [Đối chứng test nguyên bản XML](fixed-original-tests/results.xml), [log](fixed-original-tests/editor.log).
- [Danh sách từng test: CSV](all-test-results.csv).
- [Mã nguồn đã kiểm thử](tested-source/), [SHA-256 nguồn gốc/bản sao](source-hashes.json), [helper diff](helper.diff), [fixture diff](fixture.diff).

Các fixture thực được giữ ở thư mục ngắn `Evidence/VT103/r14/<id>`, địa chỉ từng run nằm trong `environment.json`. Fixture baseline dùng `Evidence/VT103/b14` cho helper. Chỉ đổi chuỗi đường dẫn cha của helper trong bản sao baseline để mọi file ghi vẫn nằm trong workspace; không thay đổi hành vi helper hoặc test khi tái hiện lỗi.

## Giới hạn an toàn và kiểm tra cuối

Editor gốc đang mở nên kiểm thử dùng bản sao `D:/Game3D-01/VT103_Work/Temp/VT103Verify` của Assets/Packages/ProjectSettings và cache. Không chạy Editor thứ hai trên project gốc. Không ghi ngoài workspace, không đổi Main, không commit/merge/push/PR.

[verification.json](verification.json) xác nhận:

- 75 file Evidence có trước: không thiếu, không thay đổi SHA-256.
- ProjectSettings gốc: 0 khác biệt so với snapshot bảo vệ.
- Ba file production đã đọc: 0 thay đổi so với `before/`.
- Sáu file code gốc/bản sao kiểm thử: 0 khác biệt SHA-256.
- Bốn file liên quan MainGate gốc/bản sao: 0 khác biệt; Git không có diff MainGate.
- Branch vẫn `individual/23001853-vt103-autosave`, HEAD vẫn `126ec82e69681445aedfa892cc7b9ec365c6c78d`.

`git diff --check` thành công. Các thay đổi ProjectSettings đang hiện trong Git là thay đổi có sẵn trước nhiệm vụ, không được reset hoặc sửa trong lần này.

Tái chạy trên bản sao đã giữ lại, chọn Phase mới chưa tồn tại:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/VT103/Run-RepairVerification.ps1 -Phase repeat-play -Platform PlayMode -ExpectedTests 45
powershell -NoProfile -ExecutionPolicy Bypass -File tools/VT103/Run-RepairVerification.ps1 -Phase repeat-edit -Platform EditMode -ExpectedTests 125
```
