# Kết quả tích hợp hành trình — 13/11/2026

Nhánh `fix/bootstrap-journey-integration`, dựa trên `origin/dev` `f933595` vừa fetch.
Không push/merge hoặc sửa các file chưa theo dõi có sẵn. Không có AGENTS.md trong
repository/các thư mục cha áp dụng khi kiểm tra. Đã đọc tài liệu Bootstrap,
MainMenu, Core, kiến trúc, Floor01, Campus và TrainingOffice trước khi sửa.

## Kết quả thực chạy

Unity Linux **6000.6.5f1** trên bản sao `/tmp/hus-integration-validation`, không mở
project gốc bằng Editor. Production Assets/ProjectSettings của bản kiểm thử đã
được đối chiếu byte với working tree và khớp nhau. Unity tự thêm Linux SDK và
Linux toolchain 1.1.1 vào manifest/lock **chỉ trong bản sao**; manifest/lock của
repository giữ nguyên. Không build standalone player.

| Kiểm tra | Kết quả | Bằng chứng |
| --- | --- | --- |
| Unity compile | Thành công; exit 0, không có C# compiler error trong lần cuối | [Native log](IntegrationEvidence/native-final.log), có compile probe cuối cùng trước khi chạy |
| Edit Mode toàn bộ suite trong Unity project | **110 passed, 0 failed, 0 skipped**, exit 0 | [EditMode.xml](IntegrationEvidence/EditMode.xml) |
| Play Mode toàn bộ suite trong Unity project | **33 passed, 0 failed, 0 skipped**, exit 0 | [PlayMode.xml](IntegrationEvidence/PlayMode.xml) |
| Native Bootstrap/menu/Single travel | **49/49 assertions**, exit 0; 0 lỗi gameplay; có lỗi Editor Search ghi riêng | [Native final](IntegrationEvidence/native-final.txt) |
| AssetDatabase audit | Sáu scene; không missing script, broken object reference hoặc reference chéo scene được phát hiện | Thực thi trước native probe, cùng [log](IntegrationEvidence/native-final.log) và nguồn `JourneyIntegrationValidation.cs` |
| Source audit | Pass: 169 GUID không trùng; sáu build path/GUID khớp; reference chính Journey hợp lệ | [source-audit.json](IntegrationEvidence/source-audit.json) |
| Whitespace và shell syntax | `git diff --check`, `bash -n tools/Integration/run-unity.sh` thành công | Kiểm tra tại working tree |

Các suite trên là suite **bên trong HUS_Escape**. Không đồng nhất chúng với test
SaveSlot ngoài project tại `Assets/Scripts/SaveSystem/Tests/` ở repository root.
Đây không phải lượt chạy toàn bộ probe gameplay lịch sử trong `tools/`.

Probe native xác nhận:

- Bootstrap vào MainMenu/Ready; ba lần kích hoạt Button Start tạo đúng một load
  Floor01/Playing. Core và loader gốc sống xuyên toàn bộ bốn gameplay loads.
- Cố tình thực thi bước đầu Start của Floor01 trước khi loader thoát Loading:
  input bị khóa, chưa tạo Session, rồi khởi tạo bình thường sau hoàn tất tải.
- Một JourneySession xuyên Floor01 → Campus → TrainingOffice → Campus; camera
  scene-local theo đúng player mới, một camera active và tọa độ hữu hạn.
- Giữ nguyên ba chìa khóa, đủ Floor quest facts, ba Campus facts và ba Office
  facts; Campus return đúng gần `(16, 1.08, 51)`. Spawn lần vào Floor/Office và
  Campus lần đầu đúng reference đã author.
- Khóa input lúc chuyển scene, mở lại input/camera và Time.timeScale=1 sau tải;
  từ chối yêu cầu travel lặp, Floor chưa hoàn thành và Office chưa đủ khám phá.
  Load path không tồn tại bị từ chối và khôi phục input của scene nguồn.

## Lỗi Console và giới hạn

**Không tuyên bố Console sạch hoàn toàn.** Native final có một exception
`ArgumentOutOfRangeException` trong `UnityEditor.Search.SearchDatabase` /
`IndexationOnStartup`, và warning Animator `AnyState -> Run` không có điều kiện.
Hai vấn đề đã được ghi trong CAMPUS_FOUNDATION.md/TRAINING_OFFICE_FOUNDATION.md
của dev trước thay đổi này. Không sửa Editor Search, Animator hoặc tắt logging.
Probe chỉ phân loại đúng stack exception Search này vào mục riêng, giữ nguyên
message/stack trong bằng chứng; mọi lỗi khác vẫn làm fail. Không ghi nhận lỗi
runtime từ gameplay trong lượt cuối. Các NUnit suite có log lỗi cố ý được
LogAssert xác nhận; không dùng những log đó để tuyên bố Console không có lỗi.

Lịch sử kiểm tra được giữ để dễ đối chiếu:

1. Chạy Unity trong sandbox bị chặn Licensing Client, chưa compile/test. Ngoài
   sandbox, Unity kết nối license và compile được project.
2. Lượt Edit Mode đầu với probe tạm dừng ở hai lỗi API của chính probe
   (`objectReferenceInstanceIDValue` obsolete, enum NewSceneMode). Đã sửa tool;
   lượt Edit Mode chính thức đạt 110/110 như XML.
3. [Native đầu](IntegrationEvidence/native-first-run.txt): 49 assertions đạt nhưng
   process exit 1 do probe coi cả lỗi Editor Search là lỗi gameplay.
4. [Native thứ hai](IntegrationEvidence/native-second-run.txt): đã tách lỗi Search,
   nhưng physics headless chưa đủ bước sau restore nên player chưa grounded;
   travel bị guard production từ chối đúng. Probe được đặt bước thời gian 1/60,
   không nới guard production. Lượt cuối đạt 49/49, exit 0.

Native probe dùng fixture hoàn thành Floor hợp lệ và QuestManager.Signal cho
các khám phá để kiểm tra tích hợp. Chưa chạy walkthrough 17 puzzle bằng input
thật, nghiệm thu hình ảnh, cảm giác camera, animation, gamepad/touch hoặc build
standalone. Không dùng save thật và không ghi checkpoint trong native probe.
Hướng dẫn chơi thử và lệnh chạy lại nằm trong
[JOURNEY_INTEGRATION.md](../TechnicalDesign/JOURNEY_INTEGRATION.md).

## File thay đổi

Dưới `HUS_Escape/Assets/_Project/`:

- `Scenes/Bootstrap/Bootstrap.unity`: serialize Start destination sang Floor01.
- `Scripts/UI/MainMenuFlow.cs`: default destination tương ứng.
- `Scripts/Utilities/JourneySceneEntry.cs`: đợi Loading, khóa input, tái sử dụng
  owner bao gồm inactive; bỏ qua Start nếu completion đã gắn scene.
- `Scripts/Utilities/JourneySession.cs`: initialize idempotent, kiểm tra owner và
  stable Core, không Attach khi loader còn bận.
- `Scripts/Core/Tests/Editor/BootstrapSceneEntryTests.cs`: catalog sáu scene.
- `Scripts/UI/Tests/Editor/MainMenuSceneTests.cs`: Floor01 destination/catalog.
- `Scripts/UI/Tests/PlayMode/MainMenuFlowPlayModeTests.cs`: kiểm tra path/state
  khi Start lặp và cập nhật đường dẫn trong failure fixture.

Ngoài Unity Assets: cập nhật `BOOTSTRAP_SCENE.md`, `MAIN_MENU.md`,
`FLOOR01_PROTOTYPE.md`; thêm tài liệu tích hợp, báo cáo/bằng chứng này và bốn file
`tools/Integration/` (source audit, runner, editor audit và runtime probe).
Giữ SampleScene, scene GUID/meta và Build Settings nguyên trạng từ dev.
