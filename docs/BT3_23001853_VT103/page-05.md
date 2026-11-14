# RED → PATCH → GREEN và mutation
## 10. Lịch sử kết quả thật

| Mốc | NUnit / process thực tế | Diễn giải |
|---|---|---|
| RED T01–T04 [E1] | 4 chạy, 0 PASS, 4 assertion FAIL; exit2 | Scaffold không ghi: T01 còn20 thay vì21, T04 còn20 thay vì22. T02/T03 thiếu ack; không chứng minh stale overwrite vật lý. |
| Review regression [E2b] | 3 PASS, 1 FAIL; exit2 | T03 mở rộng: expected Conflict, actual Committed sau GREEN đầu. |
| GREEN đã sửa [E2] | 4/4 PASS; exit0 | Gate/read-check-commit + shared ID history, không yếu hóa oracle. |
| Restore sau mutation [E3r] | 8/8 PASS; exit0 | Khôi phục guards đúng hash, không deliberate bug trong production. |
| RED integration [E5r] | 5 chạy, 5 assertion FAIL; exit2 | Chưa có autosave/revision/error behavior; không lấy compile lỗi làm RED. |
| GREEN cuối [E5s/E5p] | 11/11 Edit + 12/12 Play; exit0 | Capture V1, reserve, cancellation/fence được biên dịch và chạy. |

Patch nhỏ dùng C1–C4: gate theo path bao đọc head → luật revision/identity → commit; staging cùng directory, Flush(true), Replace/Move; finally release và chỉ cleanup scratch của request. Committed theo sau I/O thành công. Source/hash lưu theo mốc; **chưa có commit patch để dẫn thay evidence**.

## 11. Mutation: who tests the tests?
**M1 — bỏ idempotency guard:** cho retry cùng ID xuống persistence. T03 thêm assertion trước khi khóa read-only để tránh mutant chết chỉ vì sharing exception. **KILLED:** expected Duplicate, actual Committed; một assertion failure, zero errors, exit2 [E3a].

**M2 — bỏ semaphore critical section:** giữ luật nghiệp vụ, bỏ admission/release. T04 ban đầu gặp I/O Error [E3o] → **INCONCLUSIVE**, không killed/survived. Thêm DraftIoHooks instance-scoped và T08: pause r21 sau staging; nếu r22 được admit thì drain nó trước, sau đó thả r21. Oracle luôn22. Hai lượt controlled **KILLED**, expected22/actual21 [E3b]; trace durable **22 → 21**. Không dùng Thread.Sleep tạo race.

Mỗi thử nghiệm lưu source/hash rồi finally khôi phục. Bản đúng có seam lưu riêng; restored-final 8/8 PASS. Hai mutant chọn lọc bị bắt **không phải mutation score 100%** của mọi lỗi hoặc mọi lịch chạy.

## 12. AI-assisted patch review sau test
**Nhận xét đúng:** chỉ so ID với head bỏ lọt historical-ID reuse. Regression T03 expected Conflict/actual Committed [E2b] chứng minh lỗi; shared committed-ID memory sửa nó [E2]. Review do cùng trợ lý thực hiện, không giả là reviewer độc lập.

**Nhận xét thiếu evidence tại thời điểm review:** “Semaphore + Task.Run có thể làm p95 vượt 3,5 s”. Đây là giả thuyết, không phải lỗi đã xác nhận; lúc đó chưa có benchmark120. P01 Bước5 sau đó đạt trong .NET console, vẫn không chứng minh SLA Unity sau integration. Không tối ưu speculative dựa trên nhận xét này.