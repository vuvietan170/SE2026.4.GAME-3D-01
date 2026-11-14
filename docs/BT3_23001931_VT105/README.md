# BT3 — Vũ Viết Tấn — 23001931 — VT-105

> Source hiện ở [assignments/VT105](../../assignments/VT105/README.md); báo cáo/PDF/evidence bên dưới giữ đường dẫn lịch sử của bài nộp. Harness đã được cập nhật.
> `experiments.py` ghi output mới vào `artifacts/assignment-vt105/` (hoặc `VT105_EVIDENCE_DIR`), không ghi đè evidence lịch sử.


Đầu ra: [BT3_23001931_VT-105.pdf](BT3_23001931_VT-105.pdf),
[bản nguồn báo cáo](REPORT.md), [plan trước code](PLAN.md), [logs](evidence/).

Branch: https://github.com/vuvietan170/SE2026.4.GAME-3D-01/tree/assignment/23001931-vt105

Baseline `c2cb34f1b2a8d5fe2b4704678df4bedabc93e012`.
Test-first/RED `2fd8b9f`; patch/GREEN `6ce1213`.
Không merge trước review nhóm. Báo cáo/reflection do AI hỗ trợ soạn; sinh viên cần
đọc, kiểm tra và tự chịu trách nhiệm trước khi nộp Google Classroom.

## Chạy từ repository root

Yêu cầu .NET SDK 8, hoặc Unity 6000.6.5f1 có SDK đi kèm; không tải NuGet.
Nếu SDK ở vị trí khác, export `DOTNET=/đường/dẫn/tới/dotnet`.

```bash
bash tools/VT105/run.sh
bash tools/VT105/run.sh --reverse
bash tools/VT105/run.sh --load
python3 tools/VT105/experiments.py
```

Kỳ vọng: 22/22 pass; reverse/repeat pass; load 150 tác vụ có p95 <= 4000ms.
Script experiments chạy mutation ở repository tạm: 3/3 auth tests fail (exit 1)
là kết quả mong đợi; script tổng thoát 0 khi mutation bị phát hiện và GREEN cuối pass.
Script ghi lại evidence; số đo và hash mutation tạm sẽ thay đổi mỗi lần chạy.

Sửa duy nhất `tools/VT105/policy.json` từ 24 thành 48, chạy lại test:
TC14 kiểm tra cùng save 25 giờ đổi từ bị từ chối sang khôi phục. Đây là composition
root của harness; chưa có loader cấu hình hay Save UI trong Unity.
Production nhận `SaveSlotPolicy` bất biến; caller hiện hữu dùng default 24h.

Harness compile trực tiếp source C# production bằng project standalone, không giả lập
lại logic nghiệp vụ. NUnit cũ ngoài Unity root có lỗi cú pháp và không được chạy.
Không có dependency mới, không thay manifest/framework/scene. Không có số đo
line/branch coverage tự động; xem risk/decision coverage và giới hạn trong báo cáo.

Tái tạo PDF (chỉ cần nếu chỉnh báo cáo):

```bash
python3 docs/BT3_23001931_VT105/generate_report.py
```

Dùng ReportLab có sẵn trên máy soạn báo cáo và NotoSans trên Linux; đây là công cụ
tạo tài liệu, không là dependency game hoặc harness. Main content 7 trang;
AI Usage Log và log thực thi nằm trong phụ lục.
