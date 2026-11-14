# Phụ lục B · Trích log và metadata
Giá trị từ artifact sẵn có; **không phải test chạy mới**. Phân biệt NUnit, exit và timeout. Không nhúng toàn editor.log vì dài và có dữ liệu môi trường không cần thiết.

| Artifact | Trích kết quả |
|---|---|
| E1 RED | 4 chạy; 0 PASS; 4 assertion FAIL; exit2; timeout=false |
| E2b regression | T03 expected Conflict, actual Committed; 3 PASS/1 FAIL |
| E2 GREEN | 4/4 PASS; exit0; timeout=false |
| E3a M1 | T03 expected Duplicate, actual Committed; một assertion failure, zero errors; exit2 |
| E3o M2-original | Một Error, zero assertion failure; INCONCLUSIVE |
| E3b M2 controlled | T08 expected22, actual21; exit2; source restored exactly |
| E3r restored | 8/8 PASS; exit0 |
| stability-1 | NUnit1 PASS, process−1 sau external stop; không clean PASS |
| E5s / E5p | 11/11 và12/12 PASS; exit0, timeout=false cả hai |

## Trace schedule M2 thực tế
```
Newer completed: Committed
Durable before old release: 22
Durable after both writes: 21
```

## Snapshot r2 đối chiếu G03
Envelope formatVersion1, revision **2**; scene Floor01_Prototype; facts [item:Key_A101, room:A101]; inventory [Key_A101]. Cửa Key_A101 locked=false/open=true; A102/LAB locked=true/open=false. SHA-256 payload khớp fingerprint. SHA-256 toàn file copy:

`19b96a1239784b4d05bdbae29c6098f721b4e84383e13eb170adf8a2276fd01d`

Đây là mẫu r2 lúc tổng hợp, không phải payload r1/cặp hash trước-sau idle. G04 “Hash không đổi=True; LastWriteTime không đổi=True” do người dùng báo, chưa có hai mẫu gốc. G01 thiếu kiểm Console; G05–G07 manual NOT EXECUTED.

## Lệnh tham khảo — không chạy lại ở Bước8
```
powershell -File tools/VT103/Run-Integration.ps1
  -Phase store-check -Platform EditMode -ExpectedTests 11
  -TestFilter HUSEscape.Persistence.Autosave.Tests
powershell -File tools/VT103/Run-Integration.ps1
  -Phase gameplay-check -Platform PlayMode -ExpectedTests 12
powershell -File tools/VT103/Performance/Run-Performance.ps1
```

Xuống hàng chỉ phục vụ trình bày; command.txt trong mỗi run lưu lệnh gốc. Rerun tạo evidence mới, không ghi đè thư mục cũ. Bước8 chỉ đọc/đối chiếu; không chạy regression, benchmark hay mutation mới.