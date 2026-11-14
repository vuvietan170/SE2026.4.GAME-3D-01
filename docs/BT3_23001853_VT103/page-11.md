# Phụ lục B · Chỉ mục source và artifact
Path tương đối từ **D:/Game3D-01/VT103_Work**; gốc evidence **Evidence/VT103/**. Alias rút gọn để bảng không tràn, không phải link/commit mới.

| Alias | Artifact gốc |
|---|---|
| E1 | red-20261113-170710/results.xml, process.json, assertions.json |
| E2b | green-20261113-173132/results.xml — historical-ID regression |
| E2 | green-20261113-173233/results.xml, red-to-green.diff |
| E3a | mutation-20261113-175040/M1/: mutation.diff, results.xml, assertions.json |
| E3o | mutation-20261113-175040/M2-original/results.xml — Error, INCONCLUSIVE |
| E3b | mutation-20261113-175040/M2-controlled-trace/: results.xml, test-data/**/schedule.txt |
| E3r | mutation-20261113-175040/restored-final/: results.xml, process.json; guarded-with-seam/ |
| E4 | performance-20261113-182949/: summary.json; measured/{P01,P02}/round-*/samples.csv |
| E4+ | Cùng E4: independent-recomputation.json, post-process-readback.json, coverage-availability.json, source-hashes.json |
| E5r | integration-20261113-185347/red-20261113-185509/results.xml |
| E5s | integration-20261113-185347/store-final-20261113-191008/: results.xml, process.json |
| E5p | integration-20261113-185347/gameplay-green-final-20261113-191142/: results.xml, process.json |
| E6 | Thư mục report trong evidence-root.txt cạnh PDF: inventory.json, manual-evidence-provenance.json, observed-draft-current.json, protected-evidence.json |

**C1/C2/DraftIoHooks:** HUS_Escape/Assets/_Project/Scripts/Utilities/Autosave/  
**C3/C4:** HUS_Escape/Assets/_Project/Scripts/Utilities/  
**Q:** HUS_Escape/Assets/_Project/Scripts/Puzzle/QuestManager.cs

**T/S:** Autosave/Tests/Editor/ dưới Utilities; **I:** Autosave/Tests/PlayMode/Floor01AutosaveIntegrationTests.cs. Tên method giữ ID T01–T08, S01–S03, I01–I12; XML có fullname/stack. **P:** tools/VT103/Performance/Program.cs.

**Sáu báo cáo:** docs/TechnicalDesign/VT103_TEST_SPEC.md, VT103_RED_REPORT.md, VT103_GREEN_REPORT.md, VT103_MUTATION_REPORT.md, VT103_PERFORMANCE_REPORT.md, VT103_GAMEPLAY_INTEGRATION_REPORT.md. “Chưa có gameplay” ở Bước5 là trạng thái khi đó, không phải hiện trạng Bước8.

**Draft thật:** C:/Users/ASUS/AppData/LocalLow/DefaultCompany/HUS_Escape/floor01-draft-v1.json. Chỉ copy sang E6, không sửa file gốc. Không có screenshot trong evidence kiểm kê.

**Git:** đã xác minh nhánh và baseline local. Truy vấn ban đầu lỗi mạng; lượt git ls-remote được cấp quyền mạng sau đó exit 0, không có nhánh cá nhân tương ứng. Link branch/PR vẫn PENDING. Ba file ProjectSettings modified trước tổng hợp được bảo toàn; không reset hoặc khẳng định working tree hiện tại sạch.
