# Phụ lục A · AI Usage Log (2/2)
| Giai đoạn | Công cụ AI | Prompt chính đã có | Output được dùng | Kiểm tra, sửa/bác bỏ | Evidence |
|---|---|---|---|---|---|
| Bước5 performance | Codex | 120 same-path/independent-path, ba lượt, nearest-rank; không sửa production | .NET harness, raw CSV/recomputation, risk matrix | Trợ lý đối chiếu720 mẫu; P02 peak21/20/21; không khai coverage%. Người dùng nhắc lại p95 đúng trong Bước8 | E4; PERFORMANCE_REPORT |
| Bước6 integration | Codex | I01–I05 RED trước patch; Floor01; bảo vệ manual và restore | C3, revision reserve, cancellation/fence; test liên quan | Fixture60Hz, bỏ wall-time oracle nhảy, rút ngắn path T03; giữ lượt lỗi; không khai full E2E | E5; GAMEPLAY_INTEGRATION_REPORT |
| Bước7 nghiệm thu | Codex + người dùng thao tác | Kiểm wiring/path, G01–G07; không giả manual PASS | Preflight auto-add Awake, checklist/metadata guide | Người dùng xác nhận một phần G01, r1→r2 và idle10s; manual G05–G07 chưa chạy; chưa có ảnh/payload r1 đủ | Hội thoại Bước8; E6 |
| Bước8 báo cáo | Codex | Source/evidence; PDF≤8 trang chính, reflection250–350 từ; không sửa code/push | Markdown/PDF nháp; inventory/hash; quality gate | Trợ lý đối chiếu XML/process/CSV, draft r2; **sinh viên chưa duyệt bản cuối/reflection trong phiên này** | E6; report-quality.json |

## Những gì không khai đã làm
Không có định danh model AI được xác thực; không đoán tên model. Không khai đã có screenshot, branch remote, PR hoặc commit patch. Không biến nhận xét AI thành xác nhận của giảng viên.

Reflection ngôi thứ nhất có AI hỗ trợ, cần sinh viên đọc và chịu trách nhiệm trước nộp. AI Usage Log công bố việc này; không tạo chữ ký hoặc xác nhận duyệt thay sinh viên.

Nhận xét về historical ID, barrier race và performance giả thuyết đã tồn tại trong báo cáo GREEN/MUTATION gốc. Phân loại “đúng”/“thiếu evidence” dựa trên artifact, không sáng tác reviewer comment để đủ rubric.

Bước8 chỉ tổng hợp tài liệu; không khai test/gameplay mới từ thao tác xuất PDF. Kết quả manual người dùng cung cấp giữ nguồn riêng, không ghi vào NUnit như thể runner thực thi.