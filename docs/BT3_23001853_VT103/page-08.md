# Reflection và rà soát trước nộp
## 17. Reflection — bản nháp theo góc nhìn sinh viên
<!-- REFLECTION_START -->
Qua bài này, tôi tin tưởng cơ chế autosave hơn vì đã quan sát được cả hành vi đúng và cách hệ thống thất bại. Ban đầu, bốn test xanh dễ khiến tôi nghĩ phần chống ghi đè đã đủ. Tuy nhiên, trường hợp tái sử dụng request ID sau khi head thay đổi vẫn được chấp nhận. Assertion bổ sung làm lỗi hiện rõ và buộc tôi phân biệt revision với identity.

Điều quan trọng nhất tôi học được là test PASS chỉ có ý nghĩa trong phạm vi oracle và lịch chạy đã kiểm tra. Hai tác vụ cùng khởi phát không bảo đảm chúng xen kẽ tại thời điểm nguy hiểm. Khi bỏ semaphore, lỗi I/O không phải bằng chứng rằng test bắt được stale overwrite. Lịch chạy có kiểm soát giúp tôi nhìn thấy head từ 22 giảm về 21 và giải thích được vì sao test thất bại.

Mutation tăng confidence vì những guard cần thiết không chỉ tồn tại trong code mà còn được một assertion bảo vệ. Tuy vậy, hai mutant bị phát hiện không chứng minh mọi lỗi đã được bao phủ. Benchmark cũng chỉ cho thấy ba lượt tải cụ thể đạt ngưỡng; nó không thay thế đo trên Unity với capture, animation và tương tác thật.

Tôi còn thiếu nghiệm thu đầy đủ Console, payload của revision đầu và các thao tác manual Save/Continue khi autosave lỗi. Cross-process, crash recovery và lịch sử request qua restart cũng chưa được chứng minh. Tôi cần giữ những khoảng trống này trong báo cáo thay vì dùng số test xanh để che chúng.

Bước kiểm tra tiếp theo có giá trị cao nhất là hoàn tất G05–G07 trong một thư mục save thử riêng, lưu metadata trước và sau cùng log lỗi. Cách này kiểm tra đúng điểm giao giữa thao tác người chơi, manual save và autosave mà vẫn bảo vệ dữ liệu thật.
<!-- REFLECTION_END -->

## 18. Quality gate và các việc chưa thể đóng
| Yêu cầu bài làm | Nơi đối chiếu / trạng thái |
|---|---|
| Danh tính, cam kết, repo/baseline/link | Trang1; branch/PR PENDING; không hash patch giả |
| AC, invariant, NFC, AI plan/review | Trang1–3,5; hai đề xuất sửa/bác bỏ có evidence |
| Decision table; ≥10 case đủ sáu cột | Trang3–4; 17 hàng case/nhóm; không coi Planned là PASS |
| RED–PATCH–GREEN; mutation; flaky | Trang5–6; tách assertion/error, exit và shutdown hang |
| Performance, coverage, traceability | Trang6–7; đúng workload/peak/p95; coverage NOT AVAILABLE |
| Reflection, AI log và chứng cứ | Trang8; phụ lục A/B; số từ được kiểm khi xuất PDF |

**Chưa có rubric gốc của giảng viên trong nguồn cung cấp.** Bảng đối chiếu dựa trên yêu cầu Bước8, không tuyên bố đã đọc rubric chưa có. Trước nộp: sinh viên duyệt reflection/cam kết; bổ sung rubric và manual evidence còn thiếu; chỉ sau khi được phép push mới cập nhật link/commit. Không sửa log cũ để khớp kết luận.