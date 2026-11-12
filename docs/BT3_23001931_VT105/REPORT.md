# 1. Specification và thông tin bài nộp

BÀI TẬP CÁ NHÂN 3 • VT-105 • Vũ Viết Tấn • MSSV 23001931
Team ID: SE2026-T26 • Vai trò: Nhóm trưởng
GAME-3D-01 — Campus Escape: 3D stealth game
Ngày thực hiện: 12/11/2026 (Asia/Ho_Chi_Minh).

Repository: https://github.com/vuvietan170/SE2026.4.GAME-3D-01
Baseline: c2cb34f1b2a8d5fe2b4704678df4bedabc93e012
Branch: https://github.com/vuvietan170/SE2026.4.GAME-3D-01/tree/assignment/23001931-vt105
Test-first: 2fd8b9f • Production patch: 6ce1213. Không merge trước review nhóm.

Cam kết: “Tôi chịu trách nhiệm về toàn bộ code, test và nội dung bài làm; tôi đã khai báo việc sử dụng AI.”
Bản soạn có AI hỗ trợ; sinh viên cần đọc và xác nhận nội dung trước khi nộp.

Problem statement: SaveSlotService hiện cho phép khôi phục vô thời hạn. Cần thay đổi một cấu hình nghiệp vụ tại một điểm, để thời hạn khôi phục có thể đổi mà không sửa logic ở nhiều nơi. Vertical slice chọn: restore save đã soft-delete trong module có sẵn.

| AC | Kết quả cần quan sát |
| --- | --- |
| AC1 | Policy bất biến; mặc định 24h; số giờ nguyên trong [1,720], ngoài miền bị từ chối. |
| AC2 | Owner restore được khi tuổi xóa trong [0, retention], kể cả đúng hạn. Đổi config làm đổi outcome. |
| AC3 | Quá hạn, timestamp null/tương lai, chưa xóa: không restore, không gọi Save. |
| AC4 | Chỉ owner với danh tính không rỗng được restore. Trái quyền: exception, toàn bộ dữ liệu không đổi, SaveCalls=0. |
| AC5 | Success giữ id/owner/name/payload, reset deletion, cập nhật timestamp; lỗi ghi không báo success và không đổi object đang lưu. |

Invariant: một restore trái quyền hoặc không hợp lệ không làm thay đổi trạng thái save; true chỉ có nghĩa thành công nếu repository chấp nhận và outcome đúng contract.

Constraints: không đổi framework/Unity hay thêm dependency; test không phụ thuộc wall clock/network; 150 tác vụ đồng thời, p95 ≤ 4,0 giây trong harness. Thời hạn mặc định 24h và miền 1–720h là giả định thiết kế bài cá nhân, cần nhóm xác nhận trước tích hợp gameplay.

# 2. AI-assisted plan và review trước code

Quy trình: Spec → Test → AI-assisted Change → Verify → Review. PLAN.md được commit cùng test-first trước production patch. Baseline working tree sạch; tạo branch assignment/23001931-vt105 từ commit trên trang 1.

Prompt người dùng: “đọc yêu cầu, và đọc dự án SE2026.4.GAME-3D-01 và làm”. Agent đọc toàn bộ đề VT-105 và repository local. Prompt phân rã: “Tạo patch nhỏ nhất cho cấu hình thời hạn restore save slot; bảo vệ ownership; test trước, BVA, failure outcome, mutation bỏ authorization, 150 tác vụ/p95; không đổi framework hay thêm dependency.”

| Bước dự kiến | File/đầu ra |
| --- | --- |
| Spec và review kiến trúc | docs/BT3_23001931_VT105/PLAN.md |
| Test trước, lưu RED | tools/VT105/Program.cs, Harness.csproj, run.sh, policy.json; evidence/01-red.log |
| Patch nhỏ | Assets/Scripts/SaveSystem/SaveSlotPolicy.cs và SaveSlotService.cs |
| Verify / mutation / load | tools/VT105/experiments.py; evidence/*.log và mutation.patch |
| Báo cáo và hướng dẫn | README.md, REPORT.md, generate_report.py và PDF |

Review 1 — bác bỏ Unity Test Runner cho module này: PROJECT_STRUCTURE.md chỉ rõ Unity root là HUS_Escape. Source đang ở root Assets/Scripts/SaveSystem nên không được Unity import. SaveSlotServiceTests.cs và SaveSlotMutationTests.cs còn có toán tử `= >` sai cú pháp. Quyết định: executable harness compile trực tiếp source production; giữ nguyên test cũ và không báo đã chạy chúng.

Review 2 — bác bỏ assertion chỉ true/exception: fake cũ trả cùng object trong Storage, còn RestoreAsync baseline sửa object trước SaveAsync. Persistence trả false vẫn để object bị đổi. Quyết định: chụp toàn bộ snapshot trước action; assert snapshot và SaveCalls sau denial/failure; patch copy-on-write cho restore.

Review 3 — sửa test thời gian: dùng DateTime.UtcNow và sleep tại hạn sẽ dễ flaky. Quyết định: inject Func<DateTime>, dùng UTC cố định và ±1 tick. Các test có repository riêng, không chia sẻ static Storage.

Diff review sau patch: chỉ thêm policy bất biến, constructor injection và guard/window/copy trong restore. Constructor cũ được giữ, nhưng có thay đổi nghiệp vụ có chủ ý: default restore giờ bị giới hạn 24h. Không đổi SoftDelete, scene, manifest hay framework. JSON loader thuộc composition root của harness; chưa có loader hay save UI trong Unity.

# 3. Risk-based test strategy và test cases

Risk trọng tâm: test chỉ kiểm tra status/boolean mà không kiểm tra business outcome. Unit bao phủ quyết định policy/authorization; integration dùng fake repository và file config để quan sát trạng thái lưu/active-list. Failure tests kiểm tra snapshot chứ không chỉ exception. TC05–10 là negative tests; TC11–12 là failure-path tests.

| ID | Risk / AC | Level | Precondition / input | Expected result | Ưu tiên |
| --- | --- | --- | --- | --- | --- |
| TC01 | AC1/default | Unit | Policy() | 24 giờ | P1 |
| TC02 ×6 | AC1/BVA config | Unit | 0,1,2,719,720,721 giờ | 0/721 bị từ chối; còn lại restore đúng | P0 |
| TC03 ×3 | AC2–3/BVA time | Unit | 24h−1 tick, 24h, 24h+1 tick | 2 ca đầu restore; ca cuối không ghi | P0 |
| TC04 | AC2/change config | Unit | Save 25h; policy 24 rồi 48 | Deny rồi allow; payload/visibility đúng | P0 |
| TC05 | AC4/cross-owner | Unit | intruder → save owner | Unauthorized; snapshot cũ; 0 Save | P0 |
| TC06 | AC4/blank identity | Unit | Caller=owner=null, rỗng, space | Unauthorized; không ghi | P0 |
| TC07 | AC3/null timestamp | Unit | DeletedAt=null | Conflict; không ghi | P1 |
| TC08 | AC3/future | Unit | DeletedAt=now+1 tick | Conflict; không ghi | P0 |
| TC09 | AC3/state | Unit | IsDeleted=false | Conflict; không ghi | P1 |
| TC10 | Missing slot | Unit | id=999 không có | NotFound; không ghi | P1 |
| TC11 | AC5/failure path | Integration fake | SaveAsync=false | Exception; snapshot giữ nguyên; 1 Save | P0 |
| TC12 | AC5/failure path | Integration fake | SaveAsync throws IO | IO exception; snapshot giữ nguyên | P0 |
| TC13 | Regression delete | Unit | Owner delete 2 lần; attacker | Deleted đúng, 1 Save; attacker bị chặn | P1 |
| TC14 | AC2/config file | Integration fake | policy.json 24 → 48, save 25h | Đổi outcome chỉ bằng file config | P0 |
| TC15 | AC4/guard order | Unit | Attacker vào slot chưa xóa | Unauthorized trước state conflict | P0 |

BVA: với miền nguyên [1,720], chọn min−1,min,min+1,max−1,max,max+1. Với tuổi save [0,R], chọn 0, −1 tick (timestamp tương lai), R−1 tick,R,R+1 tick. 1 tick = 100ns của DateTime; không dùng độ phân giải thời gian thật để đo biên.

15 nhóm case sinh 22 executable cases; TC06 còn kiểm tra 3 danh tính trong một case. E2E Unity/HTTP chưa cần cho slice standalone vì chưa có Save UI/API để đi qua. E2E gameplay, database thật và authentication server chưa được chứng minh; khi tích hợp cần bổ sung. Không thêm framework/API chỉ để tạo số lượng test.

# 4. Test-first evidence, patch và mutation

Baseline: c2cb34f1b2a8d5fe2b4704678df4bedabc93e012. Test-first commit 2fd8b9f chứa plan, test và RED, chưa sửa production. Lệnh: bash tools/VT105/run.sh. 01-red.log: total=22, passed=5, failed=17, exit=1. C# biên dịch và chạy được; fail do thiếu policy, restore quá hạn vẫn được, thiếu bảo vệ danh tính rỗng và persistence làm bẩn object.

Harness dùng reflection cho API injection mới, fallback constructor cũ khi policy chưa tồn tại. Vì vậy baseline thực thi production thật mà không cần stub thay logic. Các fail timestamp thành công còn phản ánh baseline chưa có clock seam; bằng chứng nghiệp vụ độc lập nằm ở TC03+1, TC04, TC07–08 và TC11–12.

Patch commit 6ce1213: SaveSlotPolicy validate [1,720]; inject immutable policy và clock; ownership guard trước state/time checks; time window inclusive; tạo SaveSlot mới giữ nguyên payload và identity trước SaveAsync. Save=false/throws không làm đổi reference cũ. Đây chưa phải transaction của database; contract dựa trên repository không tự persist khi báo lỗi.

GREEN: evidence/02-green.log total=22, passed=22, failed=0, exit=0. TC14 được làm mạnh thêm sau RED: đọc file policy và kiểm tra outcome của save 25h, thay vì chỉ kiểm tra cấu hình load được. 06-config-48.log chứng minh chỉnh file 24→48 vẫn pass với outcome mới. Không thay production giữa hai lần config.

Mutation bắt buộc: bỏ toàn bộ if kiểm tra danh tính/ownership riêng trong RestoreAsync. Script experiments.py copy source vào repository tạm, tạo branch mutation/vt105-restore-authorization và commit mutant; không sửa nhánh nộp bài. Parent/mutant hash đầy đủ nằm trong 07-mutation-lifecycle.log, diff chính xác trong 07-mutation.patch.

Lệnh mutant: bash tools/VT105/run.sh --auth-only. Kết quả: TC05-cross-owner FAIL, TC06-blank-identity FAIL, TC15-owner-check-before-conflict FAIL; total=3, passed=0, failed=3, exit=1. TC05 phát hiện attacker được restore; TC15 bắt sai thứ tự guard vì nhận Conflict thay Unauthorized. Mutation bị kill; không có mutant sống sót trong thí nghiệm này.

Sau experiment: SHA256 source trước/sau giống nhau; repository tạm bị xóa. 08-final-clean.log: 22/22 pass. Temporary mutation commit không có trong lịch sử branch nộp bài. Chỉ giữ patch/log của lỗi để người review có thể tái chạy.

Review giới hạn: clone bảo vệ object đọc được trong fake; chưa chứng minh atomicity của kho dữ liệu thật. SoftDelete hiện có lỗi tương tự khi persistence fail nhưng nằm ngoài patch restore; không tuyên bố đã sửa toàn bộ SaveSystem.

# 5. Coverage, flaky review và tải 150 tác vụ

Không có collector coverage cài sẵn trong harness; không thêm dependency để lấy số %. Line/branch coverage tự động: CHƯA ĐO. Thay thế bằng risk/decision mapping có evidence; không dùng tỷ lệ coverage đơn độc để kết luận chất lượng.

| Decision / risk | Evidence đã chạy | Giới hạn |
| --- | --- | --- |
| Policy range/default | TC01, TC02 ×6 | Giờ nguyên; không có parser số thực |
| Owner/identity; guard order | TC05,06,15; mutation killed | Caller identity chưa được authenticate |
| State/missing slot | TC09,10 | Không xác minh privacy của thông báo not-found |
| Time valid/expired/null/future | TC03,04,07,08 | Clock hợp đồng UTC; chưa test DST/system clock drift |
| Save true/false/throws + payload | TC11,12 và mọi Restored assertion | DB transaction/concurrent writes chưa đo |
| Ctor null dependency guards | Review source | Chưa có executable cases riêng |

Flaky time: clock cố định tại 2026-11-12T00:00:00Z; không sleep để vượt biên. Flaky order/shared state: seed repo riêng mỗi case, reverse order pass 22/22 và 3 lần lặp cùng thứ tự đều 22/22. Không network, không random data. Config experiment dùng finally để khôi phục file. Load có scheduler/GC jitter; không khẳng định deterministic latency.

Mô hình tải: 150 async tasks cùng đến TaskCompletionSource barrier trước khi release. Mỗi task dùng repo/save riêng, I/O delay giả lập 10ms cho Get/Save; 50 restore hợp lệ, 50 trái quyền, 50 quá hạn. Mỗi task assert persisted business outcome; tính nearest-rank p95 = mẫu thứ 143 sau khi sort 150 latency. Thời gian đo từ sau barrier đến hết assertion, bao gồm async continuation/IO, không gồm build/startup.

| Lần | Tác vụ | p95 (ms) | Max (ms) | Mục tiêu 4000ms |
| --- | --- | --- | --- | --- |
| 1 | 150 (50/50/50) | 54.007 | 54.852 | Đạt |
| 2 | 150 (50/50/50) | 76.665 | 78.412 | Đạt |
| 3 | 150 (50/50/50) | 96.007 | 108.252 | Đạt |

Kết luận chỉ cho harness: cả 3 lần đạt p95 ≤ 4,0s, không có outcome sai. Không phải 150 người chơi thật, không có HTTP/socket, disk/database, Unity rendering hay contention trên cùng slot. Không suy ra capacity production. High-value follow-up: hai request cùng slot qua repository bền vững, kiểm tra atomicity sau failure/conflict.

# 6. Traceability và AI review

Toàn bộ test mới nằm tại tools/VT105/Program.cs, chạy trên Assets/Scripts/SaveSystem/*.cs. E = evidence/ trong thư mục báo cáo. Mọi hàng có implementation commit 6ce1213; test-first gốc 2fd8b9f, baseline ở trang 1.

| AC / risk | Test → file | Commit | Kết quả / evidence |
| --- | --- | --- | --- |
| AC1 / config | TC01–02 → Program.cs | 2fd8b9f → 6ce1213 | RED fail → GREEN pass; E/01,02 |
| AC2 / business outcome | TC03–04,14 → Program.cs | 2fd8b9f → 6ce1213 | Biên và đổi file 24→48 pass; E/02,06 |
| AC3 / invalid restore | TC03+1,07–10 → Program.cs | 2fd8b9f → 6ce1213 | Không ghi; E/02-green.log |
| AC4 / authorization | TC05–06,15 → Program.cs | 6ce1213; mutant tạm | GREEN pass; mutant 3 fail; E/07-* |
| AC5 / persist + payload | Restored helper, TC11–12 → Program.cs | 2fd8b9f → 6ce1213 | Snapshot giữ nguyên khi save fail; E/01,02 |
| Regression | TC13 → Program.cs | 2fd8b9f → 6ce1213 | Delete idempotency/owner pass |
| NFR stability | Cases reverse/repeat | 6ce1213 | E/03,04-*; đều pass |
| NFR load | Load() → Program.cs | 6ce1213 | E/05-load-1..3.log; p95 đạt |

Điểm AI-generated có vẻ hợp lý nhưng chưa đáng tin ngay (1): Assert.Throws khi Save=false có vẻ đủ. Baseline vẫn làm đổi object trong fake trước khi throw. Chỉ khi assert snapshot/payload/SaveCalls mới phát hiện business outcome sai. Evidence: TC11–12 RED, sau copy-on-write GREEN.

Điểm (2): async Task.WhenAll có 150 phần tử trông như kiểm chứng tải đủ. Nếu fake trả Task.CompletedTask, toàn bộ có thể chạy tuần tự. Harness thêm barrier + I/O await 10ms, đếm đủ 150 ready trước release, và kiểm tra outcome từng task. Kết quả vẫn chỉ áp dụng cho giả lập; không chuyển thành khẳng định server/game đạt tải thật.

AI Usage Log: công cụ là Codex trong IDE, shell, Unity-bundled .NET SDK 8.0.318 và ReportLab có sẵn để tạo PDF. AI hỗ trợ plan/code/test/report; không dùng AI response làm bằng chứng correctness. Tất cả test, mutation, load có lệnh và log thực thi; hai điểm bị bác bỏ cùng evidence nằm ở trang 2. Chi tiết prompt/output/review trong Phụ lục A.

Review trước nộp: sinh viên cần đọc source và reflection, giải thích được biên inclusive, clock seam, reference mutation và giới hạn fake. Không merge nhánh này trước khi nhóm review; nộp PDF + branch link trên Google Classroom, không gửi email.

# 7. Reflection (293 words)

Bản reflection tiếng Anh do AI hỗ trợ soạn dựa trên logs thật; sinh viên cần đọc và chỉnh theo trải nghiệm cá nhân trước nộp. Không trình bày đây là lời tự thuật đã được sinh viên xác nhận.

My confidence increased when the tests stopped treating a returned boolean as proof of success. A restore now has to produce an active save with the original payload, identity and ownership, and it must become visible only to its owner. The failure experiments were especially useful: the baseline changed the repository's object even when persistence rejected the operation. Checking the stored snapshot exposed an outcome that an exception assertion alone would have missed.

Boundary testing also made the business decision precise. The exact retention limit is accepted, while one tick beyond it is rejected. A fixed clock makes those assertions repeatable without sleeping. Changing the configuration file from twenty-four to forty-eight hours changes the outcome for the same twenty-five-hour-old save. That gives more confidence than merely verifying that a settings object contains a number.

The authorization mutation provided a separate challenge to the tests. Removing the restore ownership guard caused the security tests to fail. However, killing one mutant does not establish that every possible authorization defect is covered. Identity still arrives as a string from the caller; this module does not authenticate it.

Several important claims remain unproven. The repository is an in-memory fake, the artificial delay is not a database workload, and the barrier coordinates tasks rather than real network clients. The load experiment therefore supports only the harness target. It says nothing reliable about Unity frame rates, durable writes, or races between two operations on the same save slot.

The most valuable next test would run restore against the team's eventual persistent repository while two requests target the same slot. It should verify atomicity, ownership and final persisted state after a write failure or conflict. I would request team review of the retention semantics before connecting this policy to gameplay.

Tài liệu tái lập: README.md hướng dẫn chạy harness; PLAN.md chứa assumptions và plan trước code; REPORT.md là nội dung nguồn; generate_report.py dựng lại PDF. Evidence log đầy đủ được version control. Không có screenshot thay cho code chạy được.

# Phụ lục A — AI Usage Log (ngoài nội dung chính)

| Công cụ / prompt chính | Output dùng | Review / bác bỏ / evidence |
| --- | --- | --- |
| Codex: đọc đề VT-105 và repository thật; tạo plan trước code | PLAN.md, AC1–5; chọn restore retention | Sửa giả định Unity Test Runner bằng evidence root HUS_Escape và lỗi cú pháp tests cũ. |
| Codex: patch nhỏ nhất, không đổi framework/dependency | SaveSlotPolicy + constructor injection + restore guards/copy | Diff production giới hạn 2 files; không scene/manifest; default 24h là thay đổi nghiệp vụ cần nhóm review. |
| Codex: test outcome, BVA [1,720] và retention ±1 tick | Program.cs 15 nhóm/22 cases; clock cố định | 01-red: 17 fail; 02-green: 22 pass; không dùng boolean làm evidence duy nhất. |
| Codex: failure path repository false/throws | TC11–12 snapshot assertion | Bác bỏ exception-only vì object baseline bị đổi trước save; copy-on-write restore. |
| Codex: bỏ authorization đúng một đường restore | experiments.py, mutation.patch, temporary commits | 3 auth tests fail; SHA256 source submission không đổi; final GREEN. |
| Codex: 150 tasks, p95 4s, review flaky | Barrier + fake 10ms IO; reverse/repeat/load | Không coi Task.WhenAll là đủ; p95 đo thật trong harness; giới hạn giả lập ghi rõ. |
| Codex: tổng hợp traceability/report/reflection | REPORT.md, README.md, PDF generator | Sinh viên phải đọc/xác nhận nội dung; reflection là bản AI-assisted draft, không giả nhận review độc lập. |

Không thay framework, không thêm NuGet/game package. SDK .NET nằm trong Unity Editor cài sẵn; ReportLab là công cụ tài liệu đã có. Git branch/commits tạo local cho bài cá nhân; không merge. Không gọi AI-generated nhận xét là peer review của nhóm.

Prompt triển khai chính được agent tự phân rã và ghi trong PLAN.md trước sửa production; không giả làm một lượt prompt độc lập của sinh viên. Source changes, log và diff là bằng chứng kiểm chứng, không phải câu trả lời của AI.

# Phụ lục B — Log RED / GREEN / mutation

Lệnh RED/GREEN: bash tools/VT105/run.sh. Lệnh mutant trong bản sao tạm: bash tools/VT105/run.sh --auth-only. Raw logs tương ứng: evidence/01-red.log, 02-green.log, 07-mutation.log.

```text
RED
An issue was encountered verifying workloads. For more information, run "dotnet workload update".
FAIL TC01-default-policy: Exception: Missing central SaveSlotPolicy
FAIL TC02-config-0: Exception: Expected ArgumentOutOfRangeException
FAIL TC02-config-1: Exception: Restore state/timestamp wrong
FAIL TC02-config-2: Exception: Restore state/timestamp wrong
FAIL TC02-config-719: Exception: Restore state/timestamp wrong
FAIL TC02-config-720: Exception: Restore state/timestamp wrong
FAIL TC02-config-721: Exception: Expected ArgumentOutOfRangeException
FAIL TC03-expiry--1: Exception: Restore state/timestamp wrong
FAIL TC03-expiry-0: Exception: Restore state/timestamp wrong
FAIL TC03-expiry-1: Exception: Expected InvalidOperationException
FAIL TC04-config-changes-outcome: Exception: Expected InvalidOperationException
PASS TC05-cross-owner
FAIL TC06-blank-identity: Exception: Expected UnauthorizedAccessException
FAIL TC07-null-deletion-time: Exception: Expected InvalidOperationException
FAIL TC08-future-deletion-time: Exception: Expected InvalidOperationException
PASS TC09-not-deleted
PASS TC10-not-found
FAIL TC11-save-false: Exception: Failed persistence changed stored object
FAIL TC12-save-throws: Exception: Thrown persistence changed stored object
PASS TC13-delete-regression
FAIL TC14-config-file: Exception: Restore state/timestamp wrong
PASS TC15-owner-check-before-conflict
RESULT total=22 passed=5 failed=17

EXIT_CODE=1
```

```text
GREEN
PASS TC01-default-policy
PASS TC02-config-0
PASS TC02-config-1
PASS TC02-config-2
PASS TC02-config-719
PASS TC02-config-720
PASS TC02-config-721
PASS TC03-expiry--1
PASS TC03-expiry-0
PASS TC03-expiry-1
PASS TC04-config-changes-outcome
PASS TC05-cross-owner
PASS TC06-blank-identity
PASS TC07-null-deletion-time
PASS TC08-future-deletion-time
PASS TC09-not-deleted
PASS TC10-not-found
PASS TC11-save-false
PASS TC12-save-throws
PASS TC13-delete-regression
PASS TC14-config-file
PASS TC15-owner-check-before-conflict
RESULT total=22 passed=22 failed=0

EXIT_CODE=0
```

```text
MUTATION
FAIL TC05-cross-owner: Exception: Expected UnauthorizedAccessException
FAIL TC06-blank-identity: Exception: Expected UnauthorizedAccessException
FAIL TC15-owner-check-before-conflict: InvalidOperationException: Save slot 1 is not in deleted state (Conflict).
RESULT total=3 passed=0 failed=3

EXIT_CODE=1
```

# Phụ lục C — Load và mutation lifecycle

Lệnh tái tạo toàn bộ: python3 tools/VT105/experiments.py. Script kiểm tra expected exit code; mutation fail là kết quả mong đợi, mọi suite bình thường phải pass. Lần chạy mới sẽ có latency và hash commit tạm khác.

```text
{"concurrentTasks":150,"successfulRestores":50,"deniedOwnership":50,"deniedExpired":50,"fakeIoDelayMs":10,"p95Ms":54.007,"maxMs":54.8519,"targetMs":4000,"passed":true}

EXIT_CODE=0

{"concurrentTasks":150,"successfulRestores":50,"deniedOwnership":50,"deniedExpired":50,"fakeIoDelayMs":10,"p95Ms":76.6649,"maxMs":78.4121,"targetMs":4000,"passed":true}

EXIT_CODE=0

{"concurrentTasks":150,"successfulRestores":50,"deniedOwnership":50,"deniedExpired":50,"fakeIoDelayMs":10,"p95Ms":96.007,"maxMs":108.2518,"targetMs":4000,"passed":true}

EXIT_CODE=0
```

```text
Temporary branch: mutation/vt105-restore-authorization
0b7fe74d2407eb3c6428fcf6358c6e83dc44761a mutation: remove restore authorization guard
79cd7a837f71cc08b8a7e7a82d33c50bdd203dfa experiment: unmutated production snapshot
Submission source SHA256 before: fa7c6a67848ecc49fe022c7f1cdb75ff6b8d7ab67a94d8c9dc167ee7e0ca1b26
Submission source SHA256 after: fa7c6a67848ecc49fe022c7f1cdb75ff6b8d7ab67a94d8c9dc167ee7e0ca1b26
Temporary repository removed at experiment completion.
```

```text
diff --git a/Assets/Scripts/SaveSystem/SaveSlotService.cs b/Assets/Scripts/SaveSystem/SaveSlotService.cs
index 7dd53c3..7583b1c 100644
--- a/Assets/Scripts/SaveSystem/SaveSlotService.cs
+++ b/Assets/Scripts/SaveSystem/SaveSlotService.cs
@@ -64,9 +64,7 @@ namespace CampusEscape.SaveSystem {
             }
 
             // Guard Clause: Xác thực sở hữu
-            if (string.IsNullOrWhiteSpace(currentUserId) || slot.UserId != currentUserId) {
-                throw new UnauthorizedAccessException($"User {currentUserId} is not authorized to restore slot {slotId}.");
-            }
+            // MUTATION: restore authorization removed.
 
             if (!slot.IsDeleted) {
                 throw new InvalidOperationException($"Save slot {slotId} is not in deleted state (Conflict).");
```

Reverse/repeat/config48/final-clean logs giữ đầy đủ trong evidence/03*,04*,06*,08*. Tất cả 22/22 pass. Baseline RED và patch GREEN có commit riêng; mutation thuộc repository tạm, không được push lên nhánh bài nộp.
