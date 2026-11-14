# Performance, coverage và độ ổn định
## 13. 120 tác vụ — đo đúng phạm vi
Harness **.NET 8.0.21 Release**, SDK8.0.318; Windows, NVMe/NTFS D:, i7-12700H/20 logical processors; có Unity/Hub khác chạy. Payload JSON tổng hợp32 KiB, production DraftStore thật. Mỗi scenario warm-up120 riêng, không trộn mẫu. Ba lượt ×120/scenario = **720 mẫu đo**.

Start gate chung. Stopwatch bao SaveAsync đến ack/lỗi, gồm semaphore wait, scheduling, serialization, I/O và audit read-back. Setup/payload/pre-call dispatch nằm ngoài API latency; dispatch spread có ghi riêng. **p95 nearest-rank = mẫu thứ ceil(0,95 ×120) =114** sau sắp tăng; không bỏ mẫu lỗi.

| Case/lượt | Min | Mean | Median | p95 | Max | Peak | Commit/Stale |
|---|---|---|---|---|---|---|---|
| P01/1 | 2,030 | 34,295 | 35,077 | **48,951** | 50,490 | 120 | 2/118 |
| P01/2 | 2,216 | 47,637 | 47,376 | **70,817** | 72,379 | 120 | 3/117 |
| P01/3 | 2,405 | 60,326 | 62,870 | **105,375** | 106,924 | 120 | 7/113 |
| P02/1 | 2,660 | 11,888 | 6,964 | 45,130 | 62,218 | 21 | 120/0 |
| P02/2 | 2,447 | 12,167 | 10,328 | 27,498 | 49,303 | 20 | 120/0 |
| P02/3 | 3,109 | 10,906 | 7,407 | 23,328 | 94,212 | 21 | 120/0 |

Đơn vị ms; mỗi lượt120 started/120 completed. P01 cùng path, peak120 cả ba lượt: p95 < **3.500 ms — MET trong ba lượt quan sát**. P02 có120 clients nhưng peak21/20/21, không thay P01. Task in-flight không đồng nghĩa physical write: gate serialize P01; Stale là từ chối hợp lệ. Tổng372 Committed,348 Stale; zero Duplicate/Conflict/I/O error [E4].

Mỗi ack Committed có witness đĩa; head quan sát không giảm. Read-back độc lập sau process exit kiểm363 file cuối, payload32 KiB, metadata/SHA-256. Không phải crash recovery. Đây là benchmark **trước integration**, không phải Unity Mono/gameplay/FPS; không suy ra correctness tuyệt đối hay production SLA.

## 14. Coverage, flaky tests và residual risks
**Line coverage: NOT AVAILABLE. Branch coverage: NOT AVAILABLE.** Không tìm thấy collector hỗ trợ ở các vị trí đã kiểm tra; không cài dependency lớn hoặc sửa Unity config. Risk matrix không phải phần trăm coverage.

| Rủi ro | Evidence / xử lý và giới hạn |
|---|---|
| Scheduling, shared state/order | T08 ép pre-commit schedule; fixture GUID/path riêng, hooks instance-scoped; drain trước dispose. Không hứa bao hết interleaving. |
| Editor shutdown hang | stability-1 NUnit1 PASS nhưng process bị dừng, exit−1 sau shutdown stall; **không clean PASS**, chưa rõ nguyên nhân. |
| Headless timing / long path | I08 bỏ wall-time oracle, kiểm grounded thật; fixture60 Hz. T03 long-path lỗi thì rút ngắn fixture D:, không sửa guard; giữ lượt lỗi. |
| Chưa được chứng minh | Crash/power loss; cross-process/physical alias; persisted ID history; flush-on-quit; manual G05–G07. Reserve/fence sau integration chưa benchmark lại. |
Write fence khi restore có thể chờ I/O trên main thread, chưa có hard timeout. Gate/ID cache tồn tại suốt process; chưa stress bộ nhớ với số path/request không giới hạn.
