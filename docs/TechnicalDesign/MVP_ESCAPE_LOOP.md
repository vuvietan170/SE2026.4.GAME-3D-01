# Vòng chơi MVP HUS Escape — thiết kế đề xuất

Base `dev` = `b9eba8e` (đã merge PR #11), nhánh `feat/mvp-escape-loop`.
Các hành vi mới bên dưới đều là **đề xuất cho MVP**, được triển khai để nối gameplay hiện có; không phải yêu cầu thiết kế đã được xác nhận trước đó.

## Hiện trạng được kiểm tra

- QuestManager đã có 17 mục tiêu Floor01, ba khám phá Campus và ba nhiệm vụ Phòng Đào tạo. Mục tiêu cuối trước sửa ghi Main Gate chưa triển khai.
- JourneySession đã giữ inventory/facts xuyên scene và hỗ trợ SceneLoader, checkpoint V1, New Game, Continue, rollback/recovery. Không viết lại các hệ thống này.
- PatrolGuard đã có tuần tra, Suspicious/Alert/Search/Return; chó nghe/báo động. Chưa có bắt người chơi hay Game Over. Campus có một bảo vệ người và một chó, được bind bởi JourneySceneEntry.
- Bootstrap có Core và MainMenuFlow persistent; menu Start tải Floor01. Direct scene play tạo Core thông qua JourneySceneEntry.

## Đề xuất tuyến kết thúc

`Bootstrap → MainMenu → Floor01 → Campus → TrainingOffice → Campus / Cổng chính → Chiến thắng`.

**Đề xuất:** chặng Cổng chính nằm ngay trong Campus hiện có, không thêm scene thứ tư. Sau ba nhiệm vụ Office, trở lại Campus qua cửa sảnh, đi theo đường trung tâm lên phía Bắc và nhấn E tại cổng. Cổng primitive được serialize trong scene, không tạo ở runtime; có hai trụ, dầm và song kim loại, hai biển chỉ dẫn dùng font built-in.

- Điểm tương tác khoảng **(0, 1.08, 59.3)**; bán kính **2.2 m**.
- Điều kiện thắng: một hành trình có snapshot LAB hoàn tất, ba Campus facts và ba Office facts; đang ở Campus, đứng gần đúng MainGateExit của scene hiện tại, gameplay chưa bị UI/load khóa.
- Đến sớm hiện hướng dẫn, không sửa nhiệm vụ. Cổng không tự thắng khi đi ngang hoặc Continue, cần nhấn E.
- Không thêm reward/item hoặc quest fact vào save. Save V1 giữ nguyên, không migration; save Office/Campus hoàn tất vẫn có thể Continue rồi tự đi đến cổng.

## Đề xuất bị bắt và thử lại

**Đề xuất:** chỉ bảo vệ người bắt người chơi; chó giữ vai trò nghe/báo động hiện có. Khi Alert, bảo vệ ở trong **1.8 m** theo mặt phẳng, chênh cao tối đa **1.5 m**, có tầm nhìn mới không bị vật cản và gameplay đang nhận input, PatrolGuard báo PlayerCaught một lần mỗi binding. Không bắt xuyên tường, từ xa, khi NPC bị disable hoặc khi modal đang khóa input.

JourneySession nhận sự kiện từ đúng guard đã bind của entry hiện tại. Outcome `Caught` tách khỏi lifecycle GameState, dùng gate/coordinator hiện có để pause Core/time, mở cursor và khóa nhân vật/camera. Không thêm singleton hoặc Core owner.

**Đề xuất:** nút **Thử lại khu vực / Enter** tải lại scene qua SceneLoader, giữ **tiến trình nhiệm vụ và inventory đang có**, đặt người chơi ở spawn của khu vực và khởi động lại AI. Đây là retry tại điểm vào khu vực, không phải New Game và không phải đọc save trên đĩa. Nếu muốn bắt đầu lại toàn bộ, dùng Chơi lại sau thắng hoặc trở về menu rồi Start.

Tải retry bị từ chối/thất bại khi scene nguồn còn thì giữ nguyên màn bị bắt và quyền pause để thử lại. Source đã unload thì dùng recovery checkpoint trong RAM có sẵn, không tuyên bố đã rollback scene.

## Đề xuất màn kết thúc và menu

Màn chiến thắng/bị bắt dùng IMGUI của JourneySession đang tồn tại, không thêm scene UI:

| Màn hình | Thao tác |
| --- | --- |
| Bị bắt | Thử lại khu vực / Enter; Trở về menu / Esc |
| Chiến thắng | Chơi lại từ đầu / Enter; Trở về menu / Esc |
| F5 tiến trình | Thêm nút Trở về menu, giữ các file lưu |

Outcome là dữ liệu RAM của phiên, không ghi vào save. Nhấn lặp không khởi tạo load hay owner thứ hai. Continue/Save/travel bị từ chối sau kết thúc; Chơi lại sau thắng dùng New Game, tải Floor01 và xóa tiến trình **trong phiên**, giữ file trên đĩa.

**Đề xuất:** return menu là một transaction của JourneySession, dùng SceneLoader tải MainMenu với destination Ready. Chỉ sau khi menu đã active, có đúng một MainMenuView thì mới xóa floor/memory/recovery/outcome, gỡ event và Destroy JourneySession. Core/MainMenuFlow hiện có tiếp tục được dùng; không tải lại Bootstrap để tránh tạo owner trùng. Nếu mở gameplay trực tiếp trong Editor, Session cấu hình MainMenuFlow còn thiếu trên chính Core đó trước Start.

Rejection/failure giữ lại source cùng modal/result và dữ liệu; menu thiếu view sau khi backend báo thành công phải đưa Core từ Ready về Playing trước khi phục hồi gate của source. Nếu source đã mất, recovery hiện hữu tiếp tục cho phép tải checkpoint hoặc gọi lại return menu. Menu thành công đặt clock=1, cursor mở; Start tạo một Session mới, quest/inventory rỗng. Không xóa, ghi đè hay autosave các slot.

## Checklist chơi xuyên suốt bằng Editor có đồ họa

Các mục sau **chưa được nghiệm thu thủ công** bằng kết quả batchmode:

- [ ] Mở `Assets/_Project/Scenes/Bootstrap/Bootstrap.unity`, Play, Start. Nhấn Start nhiều lần; xác nhận một Core, một JourneySession.
- [ ] Hoàn tất 17 mục tiêu Floor01: lấy chìa khóa, mở A101, đọc manh mối/giải mã tủ, A102/sắp xếp sự kiện, LAB và xác nhận hoàn thành tầng. Tab/J/F5 pause, Esc đóng vẫn điều khiển được.
- [ ] E tại lối ra T3 để đến Campus. Khảo sát sân (tự động), E ở sơ đồ và biển hướng. Thử đến cổng Bắc sớm: có hướng dẫn, chưa thắng.
- [ ] Tiếp cận bảo vệ, chờ Alert và để bị bắt; không thể chạy/nhảy/xoay camera sau khi bị bắt. Chó vẫn báo động, không tự gây Game Over.
- [ ] Thử lại: ở spawn T3 của Campus, nhiệm vụ/vật phẩm trước khi bị bắt còn nguyên, bảo vệ/chó khởi động lại. Thử Enter và chuột, không phát nhiều load.
- [ ] Tránh bảo vệ bằng chạy/crouch/nấp sau tường, tới cửa Office phía Đông Bắc (khoảng X=16, Z=52), E vào sau ba khám phá Campus.
- [ ] Office: E tại bảng thông báo bên trái; F tại tài liệu quầy; E tại máy tính bên phải. Thử máy tính trước khi đủ hai thông tin phải bị chặn. Esc đóng thông tin.
- [ ] E cửa sảnh về Campus. Inventory, nhiệm vụ Office và vị trí quay lại đúng; đi về đường giữa X≈0, lên Bắc Z≈59.3. Kiểm tra hình ảnh/font biển và va chạm cổng.
- [ ] E tại cổng: màn **CHIẾN THẮNG**, cursor dùng được, player/camera đứng yên. Enter/Chơi lại tải Floor01 với quest/inventory mới, các file save cũ vẫn tồn tại.
- [ ] Thắng hoặc bị bắt → Trở về menu/Esc → Start: không giữ nhầm mục tiêu Campus/Office; một Core và Session mới. Thử từ F5 khi chưa kết thúc nữa.
- [ ] Kiểm tra Console, camera collision, animation, bố cục màn hình ở độ phân giải thấp và gamepad vật lý. Đây là phần cần chơi tay, không kết luận từ probe headless.

## Kiểm thử tái lập

`bash tools/MvpLoop/run-unity.sh /path/to/Unity /tmp/new-mvp-validation`

Runner sao chép project, không chạy probe trên save thật. Probe kiểm tra E/F Office, E cổng và kết quả, guard Update thật, retry/menu/failure. Tuyến native dùng checkpoint Floor01 hợp lệ và Campus facts làm fixture để tập trung phần MVP mới; không tuyên bố đó là một lần người chơi tự giải toàn bộ 17 puzzle. Các mục tiêu cũ cần checklist ở trên.

Kết quả và giới hạn: [MVP_LOOP_RESULTS.md](../Development/MVP_LOOP_RESULTS.md).
