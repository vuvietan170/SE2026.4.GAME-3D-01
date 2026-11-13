# Journey và Floor save V1: transaction và phục hồi

Áp dụng từ `origin/dev` `f933595`, nhánh `fix/journey-checkpoint-transactions`.
Unity root `HUS_Escape/`, Editor `6000.6.5f1`. Không đổi save version, scene asset,
GUID scene, Build Settings, state machine Core hoặc backend production Single.
Nhánh này không đổi menu Start sang Floor01: phần tích hợp Bootstrap/menu ở
nhánh `fix/bootstrap-journey-integration` là thay đổi riêng, chưa có trên dev.

## Dữ liệu đã commit và đề nghị

`JourneySession` giữ `floor`, `campusMemory`, `officeMemory` là dữ liệu đã commit.
`Proposal` chứa checkpoint đề nghị và pose/new-game intent. `Travel` chứa đề
nghị, snapshot thuần dữ liệu của phiên nguồn và snapshot input gate trước yêu cầu.
Không ghi file khi chuyển scene, Continue, New Game hoặc phục hồi.

| Thời điểm | Dữ liệu đã commit | Actor/input |
| --- | --- | --- |
| Đọc/validate save | Giữ nguyên | Chưa thay đổi |
| Loader từ chối | Giữ nguyên, bỏ đề nghị | Trả lại gate/action/camera/cursor trước đó, có grace frame chống lọt phím |
| Loader chấp nhận, đang tải | Giữ nguyên | Khóa travel; đóng modal cũng chưa mở gameplay |
| Core Playing, Attach thành công | Commit floor/facts đúng một lần | Bind scene mới, đóng modal và nhả input đúng thứ tự |
| Load/Attach lỗi, source còn dùng được | Giữ nguyên, bỏ đề nghị | Khôi phục source và input snapshot; giữ modal đang mở hoặc hoàn tất modal người chơi đã đóng |
| Source đã unload hoặc rollback không thực hiện được | Giữ checkpoint nguồn trong RAM; không giả lập scene cũ | `RecoveryRequired=true`, cách ly actor đích lỗi, timeScale=1, cursor hiện để dùng nút phục hồi |

`FloorSnapshot` trả bản sao để caller không sửa trực tiếp dữ liệu đã commit.
Snapshot phục hồi chụp **live progress** trước khi tải, gồm cả facts người chơi
vừa khám phá, không chỉ những memory đã lưu từ chuyến trước.

`Loaded`/`Failed` chỉ xử lý path của transaction đang sở hữu. Rejection và
synchronous failure dùng cùng cleanup, không xử lý hai lần. Những yêu cầu khác
trong lúc load/apply bị từ chối; observer không được bắt đầu Continue/New Game
hoặc travel lồng bên trong local restore. UI của Floor chỉ được khôi phục qua
thông báo Restored của Floor save, không gọi lại để phát thêm notice sau Continue
chéo scene.

`JourneySceneEntry.Start` đợi Core ổn định trước khi tạo Session. Nếu có Session
đang sở hữu travel/recovery, Start giao việc bind cho transaction đó và không tự
Attach lại destination đã bị Loaded từ chối. Destination bị cách ly sẽ dừng các
coroutine entry và disable entry để không được Start lại theo thứ tự callback khác. Owner search gồm cả inactive để
không tạo owner mới khi owner cũ chỉ bị tạm disable. Initialize idempotent,
không nhận Core còn Loading, không đăng ký handler trùng.

## Áp dụng và rollback Floor

`Floor01SaveSystem.RestoreWithRollback` validate toàn bộ dữ liệu logic trước
khi Apply, chụp trạng thái cũ, giữ trạng thái enabled của CharacterController.
Apply dùng các hook restore im lặng của Inventory/Quest/door/puzzle/pickup, không
đi qua pickup, reward hay animation tương tác.

Vị trí được kiểm tra bằng capsule overlap và ground support. Floor kiểm tra sau
khi khôi phục hình học cửa/puzzle, vì hình học đích có thể khác phiên nguồn.
Checkpoint bị chắn dùng spawn; cả hai đều không an toàn thì undo. Undo dùng
**pose cũ chính xác**, không chạy lại policy fallback của checkpoint lỗi.
`LastLoadUsedFallback` và `RestoreCount` được giữ nguyên nếu restore thất bại.
Nếu undo cũng ném lỗi, service đặt `RecoveryRequired`, giữ cả hai exception và
Journey chuyển sang tải lại snapshot nguồn thay vì báo đã rollback thành công.

`Restored` là thông báo **sau commit**. Từng subscriber được cô lập; subscriber
ném lỗi được ghi warning và các subscriber sau vẫn nhận thông báo. Lỗi observer
không làm API trả false cho dữ liệu đã commit. Restore lồng bị chặn.

Continue Floor tại chỗ dùng đúng bản dữ liệu vừa validate, không đọc lại file
lần hai rồi commit một bản khác với dữ liệu đã áp dụng.

## Input, camera và UI

`GameplayInputGate.Snapshot` giữ lease cũ, suspended actions, enablement thực của
Player actions, trạng thái camera/pending camera resume, timeScale, cursor và
thời gian grace còn lại. Snapshot gắn với đúng gate, không dùng cho gate khác.
Điều này cần thiết vì source và destination tham chiếu cùng InputActionAsset:
tắt Player lỗi ở destination cũng có thể tắt action mà source đang dùng.

Khi Attach lỗi, destination được cách ly trước, rồi source mới khôi phục action
snapshot. Modal còn mở tiếp tục giữ pause/cursor của nó; modal đã đóng trong lúc
tải sẽ nhả lease sau grace frame. Camera không chạy lại ở deltaTime=0.
`QuestPuzzleUI.CanBindJourney` kiểm tra các reference UI/quest/gate trước khi
áp dụng, tránh lỗi presentation xuất hiện sau khi session đã commit.

## JSON và tương thích V1

`SaveJson.Read` kiểm tra cú pháp, duplicate keys, kiểu dữ liệu và sự có mặt của
trường trước khi JsonUtility deserialize. Không dùng default `version=1`, false
hoặc zero của JsonUtility để chấp nhận trường bị thiếu. Giữ các kiểm tra V1 cũ:
scene, ordered quest signature, items/facts duy nhất và đã biết, liên kết
key/door/reward/LAB, các ràng buộc Campus/Office, bounds, finite position/yaw.
Có Office facts thì phải có đủ ba Campus facts, giống điều kiện vào Office thật.

- UTF-8 phải hợp lệ; file lớn hơn 1 MiB bị từ chối.
- Bắt buộc các trường gameplay, gồm bool false và từng thành phần pose/yaw.
- `savedUtc` là metadata không quyết định gameplay, vẫn cho phép thiếu/null.
- Campus V1 trước Office thiếu/null `officeFacts` được đọc thành mảng rỗng.
  Save Office vẫn bắt buộc officeFacts. Đây là tương thích V1 sẵn có, không phải
  format mới; không cần migration hoặc viết lại file khi Continue.
- Vị trí nằm trong bounds nhưng có collider mới được xử lý bằng kiểm tra vật lý
  ở scene đích; không thể chứng minh an toàn vật lý của scene chưa load.
- Việc ghi giữ `.pending` và same-directory move/replace, overwrite cần xác nhận,
  giữ `.previous`. Lỗi ghi trả false, không đổi phiên; file pending không được
  coi là save hợp lệ. Các test gây lỗi ghi xác nhận byte của slot cũ còn nguyên.

## Phục hồi sau khi source không còn

`RecoveryRequired`, `RecoveryScenePath`, `LastMessage` mô tả lỗi. Persistent
JourneySession hiển thị nút **Tải lại checkpoint phiên trước**, gọi `TryRecover()`
qua SceneLoader production. Đây là một lần load Single mới và restore snapshot
trước operation, không phải undo native load. Nếu retry thất bại, snapshot vẫn
được giữ để thử lại. Yêu cầu trùng khi đang retry bị từ chối.

Snapshot phục hồi nằm trong RAM của phiên, không thay thế save trên đĩa và không
cam kết sống sau khi thoát ứng dụng. Native probe đã kiểm tra source thực sự
unload, destination lỗi, retry source thành công và tiếp tục hành trình.

## Kiểm thử

Kết quả thực thi và bằng chứng: [SESSION_TRANSACTION_RESULTS.md](../Development/SESSION_TRANSACTION_RESULTS.md).

```bash
bash tools/SessionTransactions/run-unity.sh /path/to/Editor/Unity /tmp/new-transaction-validation
```

Output phải là thư mục mới. Runner sao chép Assets/Packages/ProjectSettings,
compile, chạy Edit Mode, Play Mode rồi probe riêng. Backend điều khiển chỉ được
stage vào Core của bản sao; không có test backend trong production Assets.
Probe dùng scene author thật, fake **chỉ engine boundary** cho các failure khó
chủ động tái hiện, và có ca riêng chạy backend Single production. Probe dùng
storage override dưới output, không đụng save thật. Có thể chạy một ca bằng
`-executeMethod TransactionValidation.Run -transactionTest <TênMethod>` trên
bản sao đã stage tool. Timeout hoặc thiếu output/XML không phải pass.

Chơi thử: dùng F5 Continue khác khu vực và New Game; nhấn lặp khi tải; thử file
JSON hỏng trên slot thử; kiểm tra Esc đóng modal, camera/di chuyển/cursor sau
rejection. Khi thử lỗi đích bằng probe, xác nhận panel phục hồi và nút tải lại.
Không sửa scene asset hoặc save thật để tạo fault thủ công.
