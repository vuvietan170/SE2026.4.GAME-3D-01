# VT-105 — Spec → Test → Change → Verify → Review

Sinh viên: Vũ Viết Tấn, 23001931; SE2026-T26; GAME-3D-01.
Baseline: `c2cb34f1b2a8d5fe2b4704678df4bedabc93e012`.
Branch: `assignment/23001931-vt105`. Không merge trước khi nhóm review.

## Problem statement
SaveSlotService hiện cho khôi phục vô thời hạn. Cần cấu hình thời hạn khôi phục
save đã xóa tại một điểm, không rải hằng số trong các đường xử lý.
Giả định nghiệp vụ của bài cá nhân: mặc định 24 giờ, cho phép 1–720 giờ;
khôi phục được tại đúng hạn, quá hạn một tick bị từ chối. Đây là quyết định
thiết kế của bài, không phải yêu cầu gameplay đã được nhóm xác nhận.

## Acceptance criteria và constraints
- AC1: Chính sách bất biến, mặc định 24h, nhận số giờ nguyên [1,720]; từ chối ngoài miền.
- AC2: Owner được restore khi 0 <= now - DeletedAt <= thời hạn; cấu hình khác đổi outcome.
- AC3: Quá hạn, timestamp tương lai/null hoặc slot chưa xóa bị từ chối, không ghi dữ liệu.
- AC4: Chỉ owner có danh tính không rỗng được restore; denial giữ nguyên toàn bộ dữ liệu và không gọi Save.
- AC5: Thành công phục hồi payload/owner/id, bỏ cờ xóa và timestamp; lỗi persistence không báo thành công hay làm đổi đối tượng đọc được.
- NFR1: Không đổi Unity/framework, không thêm package/dependency; harness dùng .NET có sẵn trong Unity Editor.
- NFR2: Harness 150 tác vụ đồng thời, p95 <= 4s; không suy rộng sang server/game thật.
- NFR3: Test dùng thời gian cố định và state riêng; mutation chỉ áp lên bản sao tạm.

Invariant: mọi restore trái quyền hoặc không hợp lệ không được làm thay đổi trạng thái
save; trả true chỉ khi repository chấp nhận save và outcome đúng contract.

## Prompt và plan trước code
Prompt gốc: “đọc yêu cầu, và đọc dự án SE2026.4.GAME-3D-01 và làm”.
Prompt triển khai (agent tự phân rã): “Tạo patch nhỏ nhất cho cấu hình thời hạn
restore save slot; bảo vệ ownership; test trước, BVA, failure outcome, mutation bỏ
authorization, 150 tác vụ/p95; không đổi framework hay thêm dependency.”

1. Đọc module và kiến trúc; ghi baseline, tạo branch.
2. Viết executable C# harness biên thời gian/cấu hình, quyền và outcome; chạy baseline lưu RED.
3. Thêm SaveSlotPolicy bất biến và clock seam; giữ constructor cũ; áp policy vào RestoreAsync.
4. Copy-on-write trước Save để lỗi ghi không làm bẩn object đang được repository giữ.
5. Review diff; chạy GREEN, đảo thứ tự/lặp, mutation và load; ghi log, report PDF.

Files dự kiến: `Assets/Scripts/SaveSystem/SaveSlotPolicy.cs`, `SaveSlotService.cs`;
`tools/VT105/` chứa harness, runner, cấu hình JSON; `docs/BT3_23001931_VT105/`
chứa evidence, README, báo cáo và script tạo PDF. Không sửa Unity scenes/packages.

## Review plan dựa trên repository thật (hai điểm sửa/bác bỏ)
1. Bác bỏ giả định “chạy test SaveSystem bằng Unity Test Runner”: tài liệu
`docs/Architecture/PROJECT_STRUCTURE.md` xác nhận Unity root là HUS_Escape;
module đang ở root Assets, không được Unity import. Tests cũ có `async() = >`
và `s = >`, không biên dịch. Chuyển sang harness compile trực tiếp hai source
production + policy nếu có; không giả vờ đã chạy NUnit/Unity.
2. Bác bỏ phương án chỉ assert true/exception: GetByIdAsync của fake cũ trả chính
object trong Storage; RestoreAsync sửa object trước khi SaveAsync trả false.
Thêm test assert toàn bộ persisted state và SaveCalls; copy-on-write chỉ đường restore.
3. Sửa phương án dùng DateTime.UtcNow + sleep ở test biên: một tick quanh hạn
có thể flaky. Inject Func<DateTime>, dùng UTC cố định, test chính xác ±1 tick.

## Scope không làm
Không có HTTP/API/auth server hoặc gameplay Save UI trong slice này. Không thêm
chúng để phục vụ bài. Không sao chép test/prompt VT-104; các file tests cũ giữ nguyên
và được ghi rõ không chạy. SoftDelete có behavior sẵn, không mở rộng policy sang đó.
