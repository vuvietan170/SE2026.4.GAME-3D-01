# Journey/Floor save consistency — 13/11/2026

Nhánh làm việc: `fix/journey-checkpoint-transactions`, tạo từ `origin/dev`
`f933595` sau khi fetch. Không tìm thấy AGENTS.md áp dụng; đã đọc mã và tài liệu
Floor save, Campus, TrainingOffice và luồng Journey trước khi sửa. Các thay đổi
package/IDE và file cá nhân có sẵn được giữ riêng, không hoàn tác.

## Lỗi xác nhận trên dev trước khi sửa

Bộ probe khởi chạy scene author thật trên bản sao Unity. Đối với rejection và
failure, chỉ engine boundary của SceneLoader được điều khiển; GameManager,
reservation, SceneLoader events, Journey, Floor save và actor thật vẫn chạy.
[Baseline đã cô lập](SessionTransactionEvidence/red-isolated.txt): **0/8 pass**,
process exit 1, cả tám ca đều thất bại tại assertion chứ không phải lỗi compile:

| Ca | Lỗi thực tế |
| --- | --- |
| Continue bị loader từ chối | Dữ liệu floor/memory đã đổi trước khi Begin trả false |
| New Game bị từ chối | Chỉ floor được trả lại, Campus/Office memory đã bị xóa |
| Load đã nhận nhưng thất bại | Session giữ dữ liệu đề nghị dù source còn tồn tại |
| Restore Campus tại chỗ không tìm được vị trí | Dữ liệu/binding current đã đổi trước khi Attach hoàn tất |
| Subscriber Restored ném lỗi | TryContinue trả false sau khi scene đã nhận dữ liệu mới |
| Journey JSON thiếu version | Default version=1 khiến file bị chấp nhận |
| JSON thiếu bool LAB đang false | JsonUtility điền false và file bị chấp nhận |
| Source bị unload rồi load thất bại | Không có trạng thái/phương thức phục hồi rõ ràng |

[Case dữ liệu liên vùng](SessionTransactionEvidence/facts-red.txt) xác nhận
Campus save có Office facts nhưng thiếu ba điều kiện Campus vẫn được nhận.
Đã thêm ràng buộc tương ứng với cổng vào Office thực tế.

## Thay đổi

- `JourneySession`: Proposal/Travel tách đề nghị khỏi dữ liệu đã commit; commit
  sau áp dụng thành công; clone snapshot public; snapshot nguồn cho recovery;
  xử lý rejection/sync failure/async failure qua cùng cleanup, đối chiếu path.
  Chặn operation lồng khi apply/observer đang chạy. New Game không xóa memory
  trước khi tải thành công. Continue Floor dùng đúng dữ liệu vừa validate.
- `Floor01SaveSystem`: validate trước Apply; undo pose cũ chính xác và metadata
  fallback/count; giữ CharacterController.enabled; báo RecoveryRequired nếu
  undo cũng lỗi. Restored chạy sau commit, cô lập từng subscriber, chặn restore
  lồng. Không thay đường cấp item/puzzle/reward.
- `GameplayInputGate`: snapshot lease, action enablement, camera/resume, cursor,
  timeScale. Khi actor lỗi bị tắt, khôi phục source sau khi action dùng chung đã
  bị OnDisable thay đổi. Giữ travel pause nếu modal được đóng trong lúc tải;
  failure hoàn tất việc đóng đó thay vì để pause không còn modal.
- `JourneySceneEntry`: đợi Core hoàn tất Loading; tái sử dụng owner inactive;
  không tự retry Attach sau khi transaction từ chối đích. Quarantine dừng Start
  coroutine và disable entry lỗi. Initialize chỉ nhận Core ổn định, không tạo
  duplicate Session hoặc đăng ký callback trùng.
- `QuestManager`/`QuestPuzzleUI`: restore cả facts lẫn cờ khu vực, preflight
  reference UI/quest/gate trước khi Apply. Không gọi UI restore lần hai khi
  Floor save đã phát Restored, tránh notice mục tiêu lặp khi Continue chéo scene.
- `SaveJson.cs` và metadata: kiểm tra JSON/schema trước JsonUtility; không thêm
  package. Save V1 giữ nguyên, Campus cũ thiếu Office facts vẫn đọc được.

Các test follow-up cũng bắt được lỗi trong lúc hoàn thiện bản sửa: reentrant
observer, shared actions bị tắt, modal-close để lại pause, Start chạy muộn tự
Attach lại đích lỗi và UI restore lặp. Bằng chứng đỏ được giữ riêng trong
`SessionTransactionEvidence/`; chúng không được dùng làm kết quả pass cuối.

## Kết quả cuối

| Kiểm tra | Kết quả thực chạy | Bằng chứng |
| --- | --- | --- |
| Unity Editor compile | Thành công, exit 0, không có C# compiler error ở lượt cuối | [final.log](SessionTransactionEvidence/final.log) |
| Probe transaction/restore/IO/recovery | **34/34 pass**, exit 0; không lỗi runtime bất ngờ | [final.txt](SessionTransactionEvidence/final.txt) |
| Edit Mode hiện có | **110/110 pass**, 0 failed/skipped, exit 0 | [EditMode.xml](SessionTransactionEvidence/EditMode.xml) |
| Play Mode hiện có | **33/33 pass**, 0 failed/skipped, exit 0 | [PlayMode.xml](SessionTransactionEvidence/PlayMode.xml) |
| Đối chiếu production source | Assets/ProjectSettings khớp byte với bản test; 170 GUID, không trùng | [SHA-256 manifest](SessionTransactionEvidence/tested-source-sha256.json) |
| Whitespace/shell runner | `git diff --check`, `bash -n` thành công | Working tree |

Kết quả máy đọc: [summary.json](SessionTransactionEvidence/summary.json).

Bản chạy: Unity Linux **6000.6.5f1**, `/tmp/hus-integration-validation`, không
chạy Editor trên project gốc. Assets/ProjectSettings của production đã được đối
chiếu byte với bản kiểm thử. Unity thêm Linux SDK/toolchain trong bản sao; các
package/IDE file của working tree có thay đổi từ trước task được giữ nguyên.
Không có C# compiler error trong lượt cuối; không thực hiện standalone build.

Các suite Edit/Play Mode trên là suite nằm trong `HUS_Escape`, không bao gồm
SaveSlot test ngoài Unity root tại `Assets/Scripts/SaveSystem/Tests/` và không
phải toàn bộ probe gameplay lịch sử trong tools. Lượt Edit Mode đầu đạt 108/110:
[XML trước sửa catalog](SessionTransactionEvidence/EditMode-before-catalog-fix.xml)
chỉ ra hai test vẫn kỳ vọng năm scene trong khi dev có sáu scene. Chỉ cập nhật
kỳ vọng thêm TrainingOffice; không đổi Build Settings hoặc destination menu.

Probe transaction có **3 ca dùng backend Single production**: vòng hành trình
với Attach lỗi sau source unload rồi recovery thật; New Game trở về Floor và
chỉ reset sau activation; Continue Floor từ Campus không xử lý restore/notice
hai lần. Các ca khác dùng scene thật với backend điều khiển tại engine boundary
để kiểm tra lỗi xác định. Không gọi fake Journey/Inventory/Quest implementation.

Các ca xác nhận:

- Rejection và accepted failure giữ floor/memory/live binding và điều khiển.
- Modal còn mở giữ pause/cursor; modal đã đóng nhả lease; khi còn loading vẫn
  khóa gameplay. Kiểm tra enabled của Move/Look, camera follow, camera hữu hạn,
  cursor visible/lock và Time.timeScale sau đường lỗi.
- Source không còn được tải: RecoveryRequired, actor lỗi bị cách ly, cursor có
  thể dùng; TryRecover thực sự load lại source, giữ facts/keys, không nhân owner.
- Restore lỗi và lỗi rollback kép; fallback khi checkpoint bị chắn; rollback
  giữ pose, fallback metadata, counter; CharacterController state được giữ.
- Continue lặp không phát progress/reward, không tăng item count hoặc respawn
  key đã thu; snapshot public không thể sửa committed state bằng tham chiếu.
- JSON sai cú pháp/version, thiếu trường, duplicate key, null inventory,
  nonfinite số, UTF-8 hỏng, file quá lớn, enum không hợp lệ, legacy Campus V1.
- Lỗi ghi Floor/Campus thực sự đến nhánh IO: `.pending` là directory. API trả
  false, byte slot cũ và phiên giữ nguyên; không dùng save thật.

## Console và giới hạn

Probe ghi lỗi runtime bất ngờ vào `RUNTIME ERRORS`, exception Editor Search vào
mục riêng `KNOWN EDITOR ERRORS`. Chỉ lỗi Core "without activating" trong lúc
cố tình complete backend failure được xem là expected. Exception khác vẫn làm
probe fail. Lượt cuối không có lỗi runtime bất ngờ. Không tuyên bố Console sạch:
Editor Search `ArgumentOutOfRangeException / IndexationOnStartup` và warning
Animator `AnyState -> Run` có sẵn vẫn được ghi nhận; observer fault cố ý trong
test sinh warning sau commit. Không tắt logging hoặc thay đổi Editor Search.

Chưa nghiệm thu hình ảnh, cảm giác camera, input phần cứng/đường đi giải đủ 17
puzzle, gamepad/touch hoặc player build. Probe headless kiểm tra state/control
contracts và có native Single; không thay thế chơi thử trên màn hình thật.
Recovery là snapshot trong RAM, không tồn tại sau khi thoát game và không phải
undo scene unload. Không có migration V2 vì định dạng V1 không thay đổi.

## File và chạy lại

Runtime dưới `HUS_Escape/Assets/_Project/Scripts/`: `Utilities/JourneySession.cs`,
`Utilities/Floor01SaveSystem.cs`, `Utilities/JourneySceneEntry.cs`,
`Utilities/SaveJson.cs` + `.meta`, `Player/GameplayInputGate.cs`,
`Puzzle/QuestManager.cs`, `Puzzle/QuestPuzzleUI.cs`.

Test catalog: `Core/Tests/Editor/BootstrapSceneEntryTests.cs`,
`UI/Tests/Editor/MainMenuSceneTests.cs`.

Tool: `tools/SessionTransactions/TransactionBackend.cs`, `TransactionProbe.cs`,
`TransactionValidation.cs`, `run-unity.sh`. Tool không nằm trong production
Assets, không thêm assembly/package dependency cho game.

Tài liệu contract mới:
[JOURNEY_SAVE_TRANSACTIONS.md](../TechnicalDesign/JOURNEY_SAVE_TRANSACTIONS.md),
có link từ `FLOOR01_SAVE_UI.md` và `CAMPUS_FOUNDATION.md`. XML, log và kết quả
được giữ trong `SessionTransactionEvidence/`. Log text chỉ bỏ whitespace cuối
dòng để dễ review; không bỏ lỗi. Lượt bổ sung assertion controller từng dừng ở lỗi API trong probe
(`CharacterController.isActiveAndEnabled`); đã sửa thành enabled + activeInHierarchy
và chạy lại cả ba process, tất cả exit 0. [Log compile của probe trước sửa](SessionTransactionEvidence/probe-compile-error.log)
được giữ riêng, không tính là test pass.

Các bản `red-initial`/`green-initial` là giai
đoạn hoàn thiện fixture (cleanup collider và scene host); `red-isolated` là
baseline tám lỗi dev đã xác nhận bằng fixture cô lập.

```bash
bash tools/SessionTransactions/run-unity.sh /path/to/Editor/Unity /tmp/new-transaction-validation
```

Nhánh này độc lập với commit tích hợp menu `a7084df` đã push ở task trước. Việc commit/push
được thực hiện theo yêu cầu riêng sau kiểm thử; không merge vào dev.
