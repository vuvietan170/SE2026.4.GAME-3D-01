# AUTOSAVE DRAFT — VT-103
### Bài tập cá nhân 3 · Software Engineering 2026

**Nguyễn Hữu Duy · MSSV 23001853 · Team SE2026-T26**  
**Dự án:** GAME-3D-01 — Campus Escape (project Unity: HUS_Escape)  
**Repository:** [vuvietan170/SE2026.4.GAME-3D-01](https://github.com/vuvietan170/SE2026.4.GAME-3D-01)  
**Baseline:** `f933595ef7d1aa810032ddd54cafaad4a438aea4`  
**Nhánh local:** `individual/23001853-vt103-autosave`  
**Link branch / PR: PENDING / PENDING.** Git ls-remote exit 0 và không có nhánh cá nhân tương ứng trên origin (14/11/2026). Chưa tạo commit triển khai hoặc PR; không tạo đường dẫn branch giả.

> “Tôi chịu trách nhiệm về toàn bộ code, test và nội dung bài làm; tôi đã khai báo việc sử dụng AI.”

## 1. Problem statement và phạm vi
Snapshot cũ có thể hoàn tất sau snapshot mới khi retry hoặc các yêu cầu lưu cạnh tranh. Ghi theo thứ tự hoàn tất có thể làm mất tiến độ mới. Change request yêu cầu **autosave draft và ngăn stale overwrite**, không yêu cầu backend hoặc chu kỳ 10 giây cố định. Slice chỉ áp dụng Floor01; ba manual slots vẫn độc lập. Main Gate chưa commit của project chính không thuộc baseline bài cá nhân.

## 2. Acceptance criteria có thể kiểm thử

| AC | Tiêu chí và oracle độc lập với thuật toán |
|---|---|
| AC1 | Snapshot hợp lệ mới hơn được commit với đúng payload; file vật lý đọc lại được. |
| AC2 | Revision thấp hơn head bị RejectedStale; byte của head đã commit giữ nguyên. |
| AC3 | Retry cùng ID/revision/nội dung là Duplicate, không ghi lại. Tái dùng ID cho yêu cầu khác hoặc cùng revision khác ID là Conflict. Retry thấp hơn head là Stale. |
| AC4 | Các writer cùng canonical path trong một process không làm revision durable giảm; sau các yêu cầu hợp lệ không lỗi, head là revision lớn nhất. |
| AC5 | Tiến độ Floor01 mới kích hoạt draft riêng khi capture an toàn; không tiến độ thì không ghi thêm. Restore/lỗi persistence không phá phiên chơi/manual slots hoặc báo Committed giả. |

## 3. Invariant và non-functional constraints
**Invariant:** giữa hai commit thành công liên tiếp cùng path, `r(k+1) > r(k)`; payload durable thuộc chính revision đó. Duplicate/reject/error giữ head. Chỉ trả Committed sau khi Move/Replace thành công.

**NFC1:** 120 tác vụ cùng path, p95 end-to-end ≤ 3.500 ms trong workload công bố. **NFC2:** Unity API chỉ ở main thread; worker nhận JSON bất biến; không đổi manual schema/slots. **NFC3:** chỉ một process và lexical Windows path; không hứa distributed lock hoặc crash durability tuyệt đối.

*Bản nháp 14/11/2026. Kết quả lịch sử, nghiệm thu thủ công và phần chưa kiểm chứng được phân loại riêng. Sinh viên cần duyệt bản cuối trước khi nộp.*
