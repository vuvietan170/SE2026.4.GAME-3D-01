# Player input và pause thống nhất

Áp dụng trên `dev` tại `289de8b`, nhánh sửa `fix/player-input-pause-consistency`.
Tài liệu này thay phần mô tả controller độc lập Core trong PLAYER_FOUNDATION.md.

## Audit trước sửa và bảo toàn asset

- SampleScene, Floor01, Campus, TrainingOffice mỗi scene dùng **một PlayerController1**, một ThirdPersonCameraFollow. Không prefab/scene nào tham chiếu PlayerController cũ. Ba prototype có GameplayInputGate; SampleScene được bổ sung gate lúc runtime.
- PlayerController chứa implementation dùng chung; PlayerController1 kế thừa để giữ component đã serialize. Không thay component, không ghi lại scene/prefab, không đổi `.meta` hiện có.
- Unity mở cả bốn scene và kiểm tra script GUID, inputActions, cameraTransform, animator, tám giá trị tuning kế thừa, một movement controller và missing scripts. `audit_assets.py` kiểm tra nguyên byte `.meta`, danh sách scene/prefab tham chiếu và toàn bộ map UI với evidence trước sửa.
- DisallowMultipleComponent trên lớp cha ngăn thêm hai controller trong cùng họ vào một GameObject. Kiểm tra owner runtime bảo vệ dữ liệu cũ có component trùng.

## Input và crouch

Gate clone InputActionAsset cho từng actor; controller/camera không tự Enable/Disable action dùng chung. Map Player tạm ngừng khi bị chặn; GameplayUI vẫn nhận lệnh đóng modal. UI map gốc được giữ nguyên cả action/binding ID.

| Chức năng | Action / phím |
| --- | --- |
| Di chuyển theo camera | Player/Move, WASD hoặc stick |
| Chạy / nhảy | Player/Sprint, Shift; Player/Jump, Space |
| Crouch | Player/Crouch, C hoặc Ctrl; giữ binding gamepad cũ |
| Tương tác / lấy vật phẩm | Player/Interact E; Player/Pickup F |
| Inventory / journal / tiến trình | GameplayUI/Inventory Tab; Journal J; Save F5 |
| Đóng / xác nhận | GameplayUI/Cancel Esc, gamepad East; Submit Enter, gamepad South |
| Puzzle | GameplayUI/Digit0..9 (cả numpad), SequenceA/B/C, Backspace, Clear Delete |

**Crouch chỉ đổi tư thế Animator và tốc độ**, giữ nguyên height/center của CharacterController. Không có khả năng chui qua khe thấp; không cần logic nới collider khi đứng lên. Jump bị chặn khi crouch, va đầu dừng vận tốc đi lên. Speed, IsCrouching, Jump và animation do interaction kích hoạt vẫn được giữ.

## Quyền giữ pause

GameplayInputGate quản lý lease theo owner, không dùng một bool chung cho mọi UI. InventoryUI và QuestPuzzleUI giữ/release lease riêng; release một modal không mở gameplay của modal khác. Modal trên cùng nhận bàn phím và hiển thị; Cancel được consume trong frame để không đóng modal phía dưới. Hotkey không tự mở thêm modal khi đang bị chặn. Sau lần release cuối, gate giữ chặn hết frame đóng và frame kế tiếp, tránh nút Cancel gamepad đồng thời kích hoạt Crouch.

GameplayPauseCoordinator nằm trên Core root hiện có, tồn tại qua unload. Nó đồng bộ Playing/Paused với GameManager và lưu/khôi phục Time.timeScale, cursor lock, cursor visibility. Không tự resume một pause có trước do owner bên ngoài tạo. Không tạo Core hoặc JourneySession mới. Player assembly tham chiếu Core để bind rõ dịch vụ của Core đang sở hữu Journey.

Startup hold chỉ chặn input đến khi Core Loading hoàn tất. Journey giữ lease pause riêng trong chuyến đi; đóng modal không release lease này. Khi tải thành công, source được giải phóng, target bind cùng Core và release startup hold. Khi tải lỗi còn source, snapshot khôi phục lease/action/camera, bỏ lease của modal đã đóng. Trường hợp source đã mất dùng recovery của Journey thay vì giả lập rollback scene. Disable gate đóng UI qua Deactivated và giải phóng quyền pause; enable lại có grace trước khi nhận gameplay.

## Checklist chơi thử có hình ảnh

Các mục sau cần xác nhận thủ công trong Editor có đồ họa, không suy ra từ chạy headless:

- [ ] Mở Bootstrap, Play, Start; thử WASD khi xoay camera, Shift, Space, C/Ctrl. Kiểm tra animation và capsule không thay đổi khi crouch.
- [ ] Tab mở/đóng inventory; J mở/đóng journal; F5 mở menu tiến trình, Esc đóng. Khi mở, nhân vật/camera đứng yên, cursor thao tác được; khi đóng, điều khiển trở lại.
- [ ] Tương tác E/F; nhập puzzle bằng hàng số/numpad, A/B/C, Backspace/Delete, Enter; chuột bấm xác nhận và đóng hoạt động.
- [ ] Gamepad East đóng modal và giữ nút: không đồng thời crouch. Submit gamepad không làm nhân vật nhảy khi UI xử lý xác nhận.
- [ ] Disable/enable InventoryUI, QuestPuzzleUI, GameplayInputGate và controller trong Play Mode; đóng UI không để timeScale=0 hoặc Look/Move bị tắt vĩnh viễn.
- [ ] Kiểm tra modal lồng nhau qua fixture/API: Esc chỉ đóng lớp trên, lớp dưới vẫn giữ pause; lần đóng cuối mới resume.
- [ ] F5 Continue/New Game khi chuyển khu vực; Floor01 → Campus → TrainingOffice → Campus. Kiểm tra spawn, inventory, nhiệm vụ, camera, cursor, Console và input sau mỗi bước.
- [ ] Đối chiếu hình ảnh camera collision cạnh cửa/trần; không dùng kết quả headless để kết luận chất lượng hình ảnh hay cảm giác điều khiển.

## Chạy lại tự động

`bash tools/PlayerPause/run-unity.sh /path/to/Unity /tmp/hus-player-new-run`

Script tạo project dùng một lần, không chạy probe trên thư mục chứa save thật. Probe reset scene, dùng Keyboard/Mouse/Gamepad ảo, đi qua controller và UI thật; không thêm test scripts vào Assets của repository. Có audit serialization native, Edit Mode/Play Mode, native Journey integration và transaction failure injection. Output/log nằm trong thư mục tạm được chỉ định.

Kết quả thực thi và evidence: [PLAYER_PAUSE_RESULTS.md](../Development/PLAYER_PAUSE_RESULTS.md).
