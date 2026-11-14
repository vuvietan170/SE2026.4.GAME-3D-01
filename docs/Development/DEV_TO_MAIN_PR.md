# Mô tả PR dự kiến: dev → main

**Chỉ là nội dung chuẩn bị, chưa tạo PR.** Main được đối chiếu tại `c2cb34f`, dev tại `126ec82`. Phần chuẩn hóa trên `chore/repository-standardization` còn cần review/đưa vào dev trước khi dùng mô tả tổng hợp này; không tự merge.

**Title:** `release: HUS Escape MVP — complete journey, transactional saves and repository validation`

## Problem and resulting behavior

Đưa vòng gameplay MVP từ dev vào main: người chơi khởi động Bootstrap, qua menu vào Floor01, hoàn thành Campus và Phòng Đào tạo, quay lại Cổng chính để thắng. Bảo vệ bắt có hậu quả và retry; về menu rồi Start tạo phiên mới thay vì giữ nhầm quest/inventory. Các thao tác tải, save/Continue và UI pause có cơ chế kiểm soát lỗi và giữ một Core/Session.

## Features and changes

- Floor01: 17 mục tiêu, chìa khóa/cửa, puzzle mã, sequence và LAB; Campus/Office thêm ba khám phá mỗi khu vực. Cổng primitive nằm trong Campus.
- PlayerController implementation chung, PlayerController1 tương thích giữ serialized references; move/sprint/jump/crouch/animation, camera và modal/input gate theo owner. Crouch chỉ đổi tư thế/tốc độ.
- JourneySession kết nối SceneLoader; thắng/bị bắt/retry/New Game/menu. Retry giữ live progress tại spawn; New Game đặt lại RAM, không xóa save.
- Save V1 local với validate trước apply, proposal riêng, rollback khi source còn và recovery khi source đã unload. Continue không replay reward hoặc nhân đôi item; SaveSlotService bài tập không bị mô tả thành backend game.
- README/setup/test/build chuẩn; SaveSlot assignment chuyển nguyên byte ra khỏi Unity root giả, harness .NET runnable và đường dẫn mutation cập nhật.
- Dọn render template reference hỏng và orphan test sub-assets, giữ GUID và 19 volume effect hợp lệ; audit meta/GUID/large files, `.gitignore` và đề xuất LFS theo số đo.
- Runner tạo project tạm, compile/native audit, Edit/Play Mode, gameplay probes, development build và chạy executable smoke. Log/XML/JSON/fingerprint tách khỏi source.

## Validation evidence

Kết quả chạy lại và đường dẫn log/output trong [REPOSITORY_STANDARDIZATION_RESULTS.md](REPOSITORY_STANDARDIZATION_RESULTS.md), [RepositoryEvidence](RepositoryEvidence/).

- Unity 6000.6.5f1; native import audit sáu scene và hai prefab, resolve package GUID và serialized references.
- Edit Mode 110/110; Play Mode 33/33; MVP 16/16; Journey 49 checks; PlayerPause 20/20; Transaction 34/34.
- Assignment harness 22/22; reverse/load/config/mutation experiments có kết quả riêng. Legacy NUnit lỗi cú pháp vẫn lưu làm archive, không tính pass.
- Linux development build Succeeded (0 errors, 2 warnings); executable smoke Bootstrap → menu → Floor01 → menu → phiên mới đạt, một Core/Session. Report và lần smoke fail do deferred Destroy của probe được giữ riêng; probe đã sửa và build/smoke lại. Chưa chạy Windows; cross-build không phải runtime verification.

## Limits and compatibility

- Đây là MVP/prototype, không phải art/AI/UI/audio final. Thiết kế cổng trong Campus, phạm vi bị bắt và chính sách retry được ghi là đề xuất.
- Các probe dùng fixture checkpoint và điều khiển actor; chưa thay thế một lần chơi tay tự giải 17 puzzle, kiểm tra visuals, camera feel, UI scaling, fullscreen và gamepad vật lý.
- Save V1 giữ format cũ; không cloud/account/delete-slot UI hoặc persistence AI. Scene SampleScene vẫn phục vụ test.
- Lỗi Editor Search/IndexationOnStartup và Animator warning đã có được ghi riêng; không tuyên bố mọi Console của project sạch tuyệt đối.
- Chưa có CI cloud run/license provisioning, platform release chính thức hoặc Windows player verification nếu report chỉ có Linux. Thư mục rỗng không được tính thành tính năng hoàn tất.
- Không rewrite Git history, không áp dụng LFS migration; binary hiện có vẫn là Git thường.

## Reviewer checklist trước main

- [ ] Review/đưa nhánh chuẩn hóa vào dev rồi cập nhật base/hash mô tả nếu dev có commit mới.
- [ ] Chạy lại runner trên chính candidate dev cần merge, lưu summary + build/player evidence.
- [ ] Nghiệm thu checklist [MVP_ESCAPE_LOOP.md](../TechnicalDesign/MVP_ESCAPE_LOOP.md) bằng Editor/player có đồ họa.
- [ ] Chọn target release và kiểm tra trên đúng OS; đội cấu hình runner/license CI nếu muốn required checks.
- [ ] Xác nhận các thay đổi material/package hoặc tài liệu cá nhân chưa commit không lẫn vào candidate.
