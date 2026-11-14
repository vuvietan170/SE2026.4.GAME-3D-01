# VT-105 — bài tập SaveSlot độc lập

Source trước đây ở `Assets/Scripts/SaveSystem/` tại repository root. Đã xác minh dependency trước khi chuyển: ba file chỉ dùng `System.*`; caller thực thi nằm trong `tools/VT105/Program.cs`, không có caller trong `HUS_Escape/Assets`. Namespace là `CampusEscape.SaveSystem`, không phải lớp save runtime của game.

- `SaveSystem/SaveSlot.cs`, `SaveSlotPolicy.cs`, `SaveSlotService.cs`: giữ nguyên byte source, không sửa nghiệp vụ khi chuẩn hóa.
- `tools/VT105/Harness.csproj`: project .NET 8 compile trực tiếp ba file này; không NuGet dependency và không cần NUnit/Unity.
- `SaveSystem/LegacyTests/`: giữ nguyên test NUnit lịch sử có lỗi cú pháp (`= >`, `$ "`); không được compile bởi harness và **không được tính là test pass**. Harness executable mới là entry point kiểm chứng.
- Báo cáo/PDF/evidence bài nộp: [BT3 VT-105](../../docs/BT3_23001931_VT105/README.md). Các đường dẫn cũ trong báo cáo là provenance lịch sử.

Chạy từ repository root bằng Bash, với .NET SDK 8 hoặc SDK đi kèm Unity. Có thể đặt `DOTNET=/path/to/dotnet`:

```bash
bash tools/VT105/run.sh
bash tools/VT105/run.sh --reverse
bash tools/VT105/run.sh --load
VT105_EVIDENCE_DIR=/tmp/vt105-evidence python3 tools/VT105/experiments.py
```

Harness thông thường có 22 ca; reverse/load/config 48h và mutation experiment có báo cáo riêng. Experiment mutation chỉ sửa repository tạm, có exit 1 mong đợi cho các auth tests; lệnh tổng chỉ thành công khi mutant bị phát hiện và lần chạy GREEN cuối đạt. Output mặc định tại `artifacts/assignment-vt105/`, không ghi đè evidence bài nộp.

SaveSlotService quản lý quyền sở hữu, soft-delete/restore và retention 24h (configurable). `ISaveSlotRepository` chưa có implementation lưu trữ thật; harness dùng fake in-memory. Không có adapter đến `Floor01SaveSystem`, `JourneySession`, file JSON hay UI của HUS Escape. Không coi bài tập này là cloud save/account/delete-slot đã hoàn thành của game.
