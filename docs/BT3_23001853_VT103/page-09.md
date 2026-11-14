# Phụ lục A · AI Usage Log (1/2)
Prompt dưới đây là **tóm tắt chỉ thị thật trong hội thoại**, không tái tạo transcript nguyên văn. Công cụ: trợ lý Codex; không có log xác minh model/version cụ thể. Không tự khai reviewer độc lập hoặc hành động con người chưa xảy ra.

| Giai đoạn | Công cụ AI | Prompt chính đã có | Output được dùng | Kiểm tra, sửa/bác bỏ | Evidence |
|---|---|---|---|---|---|
| Bước1 phân tích | Codex | Đọc Save/Load; lập AC/decision table; risk test theo code; chưa viết code | Kế hoạch slice Floor01, oracle revision/ID | Người dùng chốt baseline f933595, worktree riêng và scope; bác bỏ coi10s là requirement | Hội thoại Bước1–2; TEST_SPEC |
| Bước2 RED | Codex | Worktree riêng, test-first T01–T04; RED do assertion, không thiếu assembly | Scaffold, test gọi production, runner/XML | Trợ lý sửa cú pháp NUnit không tương thích, không đổi expected. Người dùng chốt concurrency/identity trước patch | E1; RED_REPORT |
| Bước3 GREEN | Codex | Patch nhỏ stale/idempotency, atomic write; bốn test; self-review sau test | C1/C2, gate/fingerprint/ack | Self-review tìm historical-ID gap; T03 Conflict/Committed. Sửa shared ID memory; không gán cho reviewer con người | E2b/E2; GREEN_REPORT |
| Bước4 mutation | Codex | Bỏ riêng hai guard; kill phải do assertion; khôi phục hash | M1/M2 diffs, T05–T08, I/O seam/trace | M2-original Error → INCONCLUSIVE; lịch event/admission thay sleep; khôi phục guard; báo shutdown stall | E3; MUTATION_REPORT |

**Ranh giới trách nhiệm:** người dùng chốt phạm vi/contract và cung cấp nghiệm thu Game View; trợ lý đọc source, hỗ trợ patch, chạy công cụ. XML/log/hash chứng minh kết quả tool. Self-review của cùng trợ lý không phải peer review độc lập.

**Risk AI:** expected không được viết thành “đúng enum code đang trả”. Oracle phải mô tả revision/payload/byte theo change request. Khi case mới làm GREEN thành RED, giữ evidence xấu và sửa implementation đúng phạm vi, không làm yếu test.

Không có prompt log nguyên bản độc lập trên đĩa cho toàn bộ các lượt. Nguồn tóm tắt là hội thoại được cung cấp; không tạo lịch sử prompt giả để lấp chỗ trống.