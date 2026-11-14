# Kiến trúc, kế hoạch và review AI
## 4. Kiến trúc thực tế và vertical slice
Save thủ công là `Floor01SaveSystem.TrySave/TryContinue/TryNewGame`; JourneySession điều phối khu vực. Floor01 có sẵn capture/validation; QuestManager phát `ProgressRecorded`, còn `RestoreFacts` khôi phục im lặng. Không cần Inventory, QuestManager hoặc API server thứ hai.

**Luồng:** Quest fact mới → debounce 0,3 s → capture/validate V1 trên main thread → reserve revision + request ID → DraftStore gate → đọc head/kiểm tra identity → staging + Flush(true) → Move/Replace → ack. Unsafe retry 0,5 s, lỗi persistence retry 2 s. Restore hủy pending và lấy write fence trước Apply; lifecycle unsubscribe/cancel, quan sát fault sau khi component bị hủy.

| Mã source | Trách nhiệm và method để kiểm tra |
|---|---|
| C1 · DraftContract.cs | Envelope formatVersion 1, revision, requestId, payload/fingerprint; request bất biến. |
| C2 · DraftStore.cs | SaveAsync/SaveUnderGate bao read–check–commit trong gate theo path; SHA-256 + exact payload; ReserveRevisionAsync, SuspendWrites. |
| C3 · Floor01Autosave.cs | Bind/Progress/Update/CaptureSnapshotAsync: adapter Floor01; debounce/retry/lifecycle, không auto-load draft. |
| C4 · Floor01SaveSystem.cs | Awake thêm một C3 trên Floor01_QuestV1 rồi bind QuestManager; CaptureForAutosave dùng CaptureForTravel; RestoreWithRollback lấy fence. |
| Q · QuestManager.cs | ProgressRecorded, Signal, RestoreFacts là điểm nối có sẵn; không viết lại quest. |

C1/C2 ở asmdef thuần BCL HUSEscape.Persistence.Autosave, noEngineReferences. C3/C4 thuộc Assembly-CSharp. Play Mode tests dùng reflection qua ranh giới predefined assembly để gọi component thật. Path đầy đủ ở phụ lục B.

## 5. Kế hoạch AI hỗ trợ, assumptions và rollback
1. Xác minh baseline; worktree riêng trên D bảo vệ Main Gate. Đặc tả oracle trước code.
2. RED T01–T04; patch nhỏ → GREEN; bổ sung identity-history sau review.
3. M1/M2 riêng, khôi phục hash; điều khiển lịch thay vì chờ race ngẫu nhiên.
4. Benchmark riêng; integration I01–I05 test-first, rồi nghiệm thu input thật.

Người dùng chốt: concurrency một process; draft riêng; 10 s không phải requirement; không Campus/Office/Main Gate. Rollback đề xuất: phục hồi bản sao/hash slice trong worktree cá nhân, giữ draft như evidence; không reset/clean/stash project chính. Không khai đã rollback toàn game.

## 6. Hai đề xuất AI được sửa/bác bỏ
**A — Cách triển khai ban đầu chỉ so ID với head: sửa.** Khi head lên 21, ID capture-20 dùng lại với payload khác tại r22 đã được chấp nhận. T03 mở rộng thật sự FAIL: expected Conflict, actual Committed [E2b]. Shared identity-history theo path sửa lỗi, cùng assertion PASS [E2]. Không suy ra ID history bền qua restart.

**B — Lần thử M2 ban đầu dùng T04/common-start barrier: thay bằng controlled schedule.** M2-original/T04 trả AggregateException/IOException, không assertion chứng minh stale overwrite [E3o]. Controlled schedule pre-replace làm T08 thấy head 22 rồi 21 [E3b]. Oracle luôn 22; chỉ lịch chạy thay đổi, không sửa expected theo implementation.