# Decision table và risk-based strategy
## 7. Decision table rút gọn có ưu tiên
Contract: một process/path, không có writer ngoài contract sửa principal. Xét từ trên xuống trong từng phần; “—” là không liên quan. Head lỗi gồm JSON/format/fingerprint sai hoặc không đọc được. ID xung đột: ID đã commit/quan sát nhưng revision/fingerprint khác; nội dung head còn so exact.

| Rule | Điều kiện phân hoạch | Expected action | File |
|---|---|---|---|
| A0 | Không có fact tiến độ mới | Không submit | Không ghi |
| A1 | Có fact mới; capture chưa an toàn | Pending, retry có kiểm soát | Không snapshot dở |
| A2 | Pending/lifecycle hủy trước commit | Cancel; không ack thành công | Giữ head |
| W1 | Request sai revision/ID/payload | Lỗi input | Giữ head |
| W2 | Request hợp lệ, token đã hủy | Cancellation | Giữ head |
| W3 | Head có nhưng lỗi/không đọc được | Lỗi, không thay âm thầm | Giữ evidence |
| W4 | Head hợp lệ, incoming r < head r; mọi ID | RejectedStale | Byte không đổi |
| W5 | Không stale; ID xung đột đã biết | Conflict | Byte không đổi |
| W6 | Head thiếu; không conflict; I/O thành công | Committed đầu tiên | r/payload cùng lưu |
| W7 | Head thiếu; không conflict; I/O lỗi | Lỗi, không Committed | Không principal dở |
| W8 | r = head r; cùng ID và nội dung | Duplicate | Không staging/replace |
| W9 | r = head r; còn lại: ID/nội dung khác | Conflict | Byte không đổi |
| W10 | r > head r; không conflict; I/O thành công | Committed mới | Replace bằng r mới |
| W11 | r > head r; không conflict; I/O lỗi | Lỗi, không Committed | Giữ commit trước |

**Hiệu chỉnh spec:** bảng ban đầu VT103_TEST_SPEC.md đặt identity trước stale, mơ hồ với retry cũ. Quyết định Bước 3 được ưu tiên: **revision thấp hơn head luôn Stale**. Bảng trên ghi rõ thay đổi contract. A2 không hứa hủy atomic replace đã bắt đầu; restore drain/fence trước Apply. Đây không phải tuyên bố mọi rule đã được test: W1/W7 riêng, acknowledgment-loss và crash còn thiếu thí nghiệm.

## 8. Chiến lược theo risk
- **Unit/contract + filesystem thật (T/S):** oracle là ack, byte, revision, payload và side effect. Seed JSON độc lập. T03 dùng lease read-only; T07 ném lỗi sau staging thật. Negative tests T02/T05/T06 kiểm byte, không chỉ exception.
- **Integration Unity (I):** Floor01 additive; Inventory/Quest/capture sản xuất, manual slot, restore, lifecycle và retry. Fixture tắt JourneySceneEntry, dùng bước 60 Hz. Không gọi đây là E2E input hay full JourneySession.
- **E2E/nghiệm thu:** F/E, HUD và metadata G01–G04; manual G05–G07 chưa chạy. Không dùng component test thay nghiệm thu thao tác người chơi.
- **Không cần trong slice:** HTTP/API server tests (không có server), cloud/distributed lock, Campus/Office/Main Gate autosave, hàng trăm regression Quest/AI hoặc kiểm lại toàn bộ 8 animation.

**Risk trọng tâm:** AI viết test khớp code. Biện pháp: expected từ AC trước patch, đọc durable data độc lập, bỏ guard có chủ đích rồi buộc assertion phát hiện vi phạm; không lấy compile error làm mutation kill.