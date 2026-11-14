# Traceability và nghiệm thu gameplay
## 15. AC/Risk → Test → File → Commit/PR → Result
File aliases: T = DraftStoreRedTests.cs; S = DraftStoreIntegrationSafetyTests.cs; I = Floor01AutosaveIntegrationTests.cs; P = Performance/Program.cs. C1–C4/Q ở trang2. **Mọi patch còn ở working tree; commit triển khai/PR PENDING.** Baseline f933595 không chứa patch autosave.

| AC / Risk | Test | File → source | Evidence / result | Commit/PR |
|---|---|---|---|---|
| AC1 revision/payload | T01, I01/I11, S01 | T/I/S → C1/C2/C3 | E5s/E5p PASS | PENDING |
| AC2 stale overwrite | T02/T08, I03 | T/I → C2 | E5 PASS; M2 killed E3b | PENDING |
| AC3 retry/ID conflict | T03/T05/T06 | T → C2 | E5s PASS; M1 killed E3a | PENDING |
| AC4 concurrency | T04/T08, S01/S03, P01 | T/S/P → C2 | E3/E4/E5; một process | PENDING |
| AC5 trigger/idle/safety | I01/I02/I06/I08 | I → C3/C4/Q | E5p PASS; G02–G04 có giới hạn | PENDING |
| AC5 manual/restore | I04/I07/I12, S03 | I/S → C2/C3/C4 | E5 PASS; manual G05/G06 chưa chạy | PENDING |
| AC5 failure path | T07, I05/I10, S02 | T/I/S → C2/C3 | E5 PASS; manual G07 chưa chạy | PENDING |
| NFC1 p95 ≤3.500ms | P01×3, P02 tham chiếu | P → C2 bản Bước5 | E4 MET; không Unity SLA | PENDING |
| Crash/cross-process/E2E | Chưa có phù hợp | Ngoài slice/còn thiếu | NOT TESTED | PENDING |

## 16. Manual acceptance: nguồn và mức kết luận
G01–G04 lấy từ **người dùng báo cáo ở yêu cầu Bước8**, không phải trợ lý tự thao tác Game View. Khi tổng hợp đã đọc/copy chỉ đọc draft r2: fingerprint đúng, hai facts và item khớp mô tả. Một mẫu r2 không tái dựng được payload r1 hoặc cặp so sánh G04.

| ID | Thao tác / Expected | Actual / mức kết luận |
|---|---|---|
| G01 | Play đúng worktree; component đúng; không lỗi mới | Đúng VT103_Work; auto-component trên Floor01_QuestV1; 0,3/0,5/2s. Chưa kiểm Console đủ → **PARTIAL**, không PASS toàn case. |
| G02 | Nhặt Key_A101 → draft hợp lệ | Người dùng báo r1 được tạo; thiếu payload r1 riêng → **PARTIAL** về toàn snapshot. |
| G03 | Vào A101 → revision/data mới | Báo1→2; file r2 có item:Key_A101, room:A101, Inventory Key_A101; cửa A101 mở, không khóa → **PASS phạm vi quan sát**. |
| G04 | Idle10s không ghi | Báo r2→2, hash/time không đổi → **PASS theo người dùng**; thiếu hai mẫu gốc. |
| G05 | F5 Save/Continue độc lập | **NOT EXECUTED manual**; I04/I12 tự động ghi riêng. |
| G06 | Restore không autosave sai/lặp | **NOT EXECUTED manual**; I07/I12/S03 không thay input acceptance. |
| G07 | I/O lỗi không phá game/ack giả | **NOT EXECUTED manual**; T07/I05/S02 là tự động. |

Không có screenshot trong evidence đã kiểm kê; không đính kèm ảnh giả. Draft không auto-restore; F5 Continue vẫn dùng manual slot. Cần sandbox save trên D trước G05–G07 vì worktree và project chính dùng chung persistentDataPath mặc định.