# PR #8 replacement after transaction merge — 14/11/2026

Base: `dev` `c76cd823d59e5aab52948221ccce0129b72d5b97`, đã merge PR #9.
Nhánh thay thế: `fix/bootstrap-integration-on-current-dev`.
Nguồn thay đổi Bootstrap: PR #8, commit `a7084df`.

## Conflict và cách giải quyết

Cherry-pick không commit PR #8 lên dev mới tái hiện conflict trong:

- `HUS_Escape/Assets/_Project/Scripts/Utilities/JourneySceneEntry.cs`.
- `HUS_Escape/Assets/_Project/Scripts/Utilities/JourneySession.cs`.

Giữ **nguyên byte** hai file từ dev mới. Phần khởi tạo/ownership mà PR #8 thêm
đã có trong #9, cùng với guard Start muộn, transaction, schema validation,
input snapshot và recovery mạnh hơn. Không mang code Journey cũ trở lại.

Runtime delta so với dev chỉ còn destination serialize trong Bootstrap và
mặc định MainMenuFlow: Start đi Floor01_Prototype qua SceneLoader. Cập nhật
đích trong hai test UI tương ứng. Catalog sáu scene đã đúng trên dev; giữ
SampleScene, Build Settings, GUID và metadata hiện có.

Tài liệu/probe từ #8 được giữ; các bằng chứng cũ được đánh dấu lịch sử. Báo cáo
này và thư mục `PR8ResolutionEvidence/` ghi kết quả **bản kết hợp** sau giải quyết
conflict, không suy diễn từ kết quả riêng của hai nhánh trước đó.

## Kết quả thực thi

Unity Linux **6000.6.5f1**, project kiểm thử cô lập
`/tmp/hus-integration-validation`; không mở Editor trên workspace chính.

| Kiểm tra | Kết quả | Bằng chứng |
| --- | --- | --- |
| Unity compile | exit 0 | [compile.log](PR8ResolutionEvidence/compile.log) |
| Edit Mode | 110/110, 0 failed/skipped, exit 0 | [EditMode.xml](PR8ResolutionEvidence/EditMode.xml) |
| Play Mode | 33/33, 0 failed/skipped, exit 0 | [PlayMode.xml](PR8ResolutionEvidence/PlayMode.xml) |
| Native Bootstrap/menu/travel | 49 assertions, exit 0 | [integration result](PR8ResolutionEvidence/journey-integration-result.txt) |
| Transaction/restore/recovery | 34/34, exit 0; gồm 3 ca native Single | [transaction result](PR8ResolutionEvidence/transaction-results.txt) |
| Source/GUID | 170 asset GUID không trùng; build path/GUID đúng; source test khớp byte | [SHA-256](PR8ResolutionEvidence/tested-source-sha256.json) |

Kết quả máy đọc: [summary.json](PR8ResolutionEvidence/summary.json).
Probe Bootstrap vẫn kiểm tra Start lặp, Start trước khi Core hết Loading, một
Core/Session, inventory/facts/spawn/camera/input và Floor → Campus → Office →
Campus. Bộ transaction vẫn kiểm tra rejection/failure, undo lỗi, JSON/IO,
Continue lặp, modal/cursor và recovery thực khi source đã unload.

Không tuyên bố Console sạch: exception Editor Search/IndexationOnStartup và
warning Animator cũ vẫn được ghi nhận. Các fault được cố ý tạo trong test được
phân biệt với lỗi runtime bất ngờ. Không build standalone hoặc nghiệm thu hình
ảnh/input phần cứng/đường giải đủ 17 puzzle. Probe travel dùng checkpoint Floor
hoàn thành hợp lệ để cô lập việc chuyển khu vực.

## Chạy lại và phạm vi

Chạy `tools/Integration/run-unity.sh` và `tools/SessionTransactions/run-unity.sh`
trên thư mục output mới để thực thi từng nhóm. Lượt kiểm chứng kết hợp này stage
cả hai bộ probe vào cùng bản sao và chạy tuần tự compile, Edit Mode, Play Mode,
`JourneyIntegrationValidation.Run`, `TransactionValidation.Run`. Không chạy
hai Editor đồng thời trên cùng project.

Thay đổi được chuẩn bị trong worktree riêng `/tmp/hus-pr8-resolution`; giữ nguyên
material, package và file cá nhân trong workspace chính. Phần điều khiển/pause
đang được khảo sát riêng không được đưa vào bản sửa conflict này. Không merge
vào dev trong quá trình tạo PR thay thế.
