# Test cases và oracle trước triển khai
## 9. Bảng kiểm thử
Mã dưới đây là **test hiện hành**, không đồng nhất T05–T08 với case “Planned” trong spec Bước 2. P0: mất dữ liệu; P1: tích hợp/khả dụng. U+FS = contract-level với filesystem thật, không fake store.

| Test ID | AC / Risk | Test level | Precondition / Input | Expected result | Priority |
|---|---|---|---|---|---|
| T01 | AC1 revision/payload | U+FS | Seed r20; r21/payload21 | Committed; file r21/payload21 | P0 |
| T02 | AC2 stale; negative | U+FS | Seed r20; request r19 | Stale; byte giữ nguyên | P0 |
| T03 | AC3 retry/ID reuse | U+FS | Retry r20/ID/payload, thêm read-only lease; head21 rồi reuse ID | Duplicate không ghi; ID đổi nội dung Conflict; retry cũ Stale | P0 |
| T04 | AC4 hai instance | U+FS | r21/r22, start barrier, dot/case path alias | Head r22/payload22, ack hợp lệ | P0 |
| T05 | AC3 ID conflict; negative | U+FS | Cùng ID, payload khác, r20/r21 | Conflict; byte không đổi | P0 |
| T06 | AC3 revision collision; negative | U+FS | Cùng r20, ID khác | Conflict; byte không đổi | P0 |
| T07 | AC5 persistence failure | FS integration | Head có; BeforeCommit ném IOException sau staging thật | Không Committed; head nguyên; sạch pending; retry được | P0 |
| T08 | AC4 lịch nguy hiểm | FS integration | Pause r21 trước replace; điều khiển admission r22 | Head luôn 22; mutant làm 21 phải assertion FAIL | P0 |
| I01 | AC5 không autosave | Unity integration | Add Key_A101 qua Inventory/Quest thật | Draft V1 đúng scene/fact/item | P0 |
| I02 | AC5 ghi khi idle | Unity integration | Fact trùng, nhiều frame không tiến độ | Count và byte không đổi | P1 |
| I03 | AC2/4 lệch thứ tự | Unity integration | Hai capture thật, commit newer trước | Older Stale; giữ newer | P0 |
| I04 | AC5 manual slots | Unity integration | Ba manual slots; autosave thật | Ba file byte-identical | P0 |
| I05 | AC5 lỗi phá phiên | Unity integration | Directory chiếm draft path, lỗi I/O thật | LastError; count0; quest/item giữ | P0 |
| I07/I12 | AC5 restore | Unity integration | Pending rồi New Game / Save+Continue | Hủy pending; chỉ restore manual, không autosave lại | P0 |
| I08/I09 | AC5 unsafe/lifecycle | Unity integration | CC airborne; re-enable nhiều lần | Hoãn rồi retry grounded; không trùng subscription | P1 |
| I10/I11 | AC1/5 head lỗi/cũ | Unity integration | Corrupt / head100 có sẵn | Giữ corrupt; reserve >100, không auto-load | P0 |
| S01–S03 | AC4/5 reserve/cancel/fence | U+FS | Hai instance; cancel sau staging; restore lease | Reserve43/44; giữ head; writer chờ fence | P0 |

**Kết quả:** T01–T08 + S01–S03 = **11/11 PASS** [E5s]; I01–I12, có I06 auto-composition = **12/12 PASS** [E5p]. Hai Editor exit0, không timeout. Không cộng các lượt rerun thành số test độc lập.

Bảng có 13 case T01–I05 riêng lẻ và 4 hàng nhóm bổ sung; mỗi hàng đủ sáu cột. Case invalid-input đầy đủ, first-write fault riêng, acknowledgment loss và crash chưa có kết quả riêng, không suy thành PASS. Tên method/fullname trong XML, path file ở phụ lục B.