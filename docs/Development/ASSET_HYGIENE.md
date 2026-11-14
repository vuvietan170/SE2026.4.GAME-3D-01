# Asset hygiene và đề xuất Git LFS

Audit source được chạy trên workspace chuẩn hóa, base dev `126ec82`; chỉ đo file thật trong Assets và tracked payload còn trên working tree, không cộng Library/Temp hay báo cáo cá nhân chưa track. Kết quả chi tiết được lưu trong RepositoryEvidence sau khi hoàn tất validation.

## Kích thước thực tế

- Importable Assets: **144.55 MiB**; đa số là FBX.
- Tracked payload đang có trên working tree tại lần đo: khoảng **146.96 MiB** (không phải toàn bộ history, không tính file normalization mới chưa track).
- **9 file ≥10 MiB**, **0 file >50 MiB**, không có file chạm 100 MiB.
- Git object store lúc audit: pack khoảng 25.92 MiB; kích thước nén của Git khác tổng kích thước checkout. Không chạy GC/history migration để thay số đo.

| File | MiB |
| --- | --- |
| `HUS_Escape/Assets/Player/Animations/Asian_University_Student@Idle.fbx` | 16.46 |
| `HUS_Escape/Assets/Player/Animations/Opening.fbx` | 16.36 |
| `HUS_Escape/Assets/Player/Animations/Unarmed Grab Torch From Floor.fbx` | 16.22 |
| `HUS_Escape/Assets/Player/Animations/Crouch Idle.fbx` | 16.09 |
| `HUS_Escape/Assets/Player/Animations/Walking.fbx` | 16.05 |
| `HUS_Escape/Assets/Player/Animations/Jumping Up.fbx` | 16.03 |
| `HUS_Escape/Assets/Player/Animations/Crouched Walking.fbx` | 16.03 |
| `HUS_Escape/Assets/Player/Animations/Running.fbx` | 16.02 |
| `HUS_Escape/Assets/Player/Asian_University_Student.fbx` | 11.53 |

**Đề xuất, chưa áp dụng:** dùng LFS cho FBX nguồn/animation lớn hoặc thay đổi thường xuyên khi đội thống nhất quota và quy trình clone/build. Có thể chọn hai nhóm `HUS_Escape/Assets/Player/*.fbx` và `HUS_Escape/Assets/Player/Animations/*.fbx` (hiện đúng chín file), thay vì đưa scene/C#/`.meta` vào LFS. Kiểm tra `git lfs pull`/materialized files trên máy build nếu áp dụng sau này.

Hiện chưa có lý do bắt buộc migration vì từng file đều dưới ngưỡng cảnh báo 50 MiB của GitHub. GitHub chặn file >100 MiB: [giới hạn chính thức](https://docs.github.com/en/repositories/working-with-files/managing-large-files/about-large-files-on-github). Ngưỡng đề xuất 10 MiB ở audit là policy xem xét của dự án, không phải giới hạn GitHub.

Không thêm filter LFS, không đổi lịch sử hoặc convert binary trong lần chuẩn hóa này. Áp dụng LFS ở commit tương lai không làm biến mất các binary version đã có trong history. Không dùng `git lfs migrate`, filter-repo hoặc force-push.

## GUID, .meta và reference

Mỗi asset/folder importable phải có `.meta` và GUID duy nhất. Dotfile `.gitkeep` được Unity bỏ qua nên không cần `.meta`. Assignment nằm ngoài Unity root nên không cần Unity metadata. Giữ nguyên GUID/serialized reference khi chuyển asset thật; lần này không di chuyển asset game.

Audit sâu phát hiện dữ liệu render template hỏng có trước chuẩn hóa:

- Bảy GUID không còn resolve dưới trường obsolete `PC_Renderer.probeVolumeResources`; trường không còn trong UniversalRendererData của package URP 17.6 đang dùng (resource hiện nằm trong pipeline global settings). Đã bỏ đúng block obsolete, giữ render settings còn lại.
- DefaultVolumeProfile chứa chín sub-asset mồ côi: bốn script GUID không tồn tại và năm script null của package/editor test components. Không nằm trong 19 component references chính, không có external fileID reference. Đã loại bỏ các block mồ côi, giữ nguyên 19 volume component hợp lệ, main fileID và `.meta` của profile.
- [Evidence native trước sửa](RepositoryEvidence/native-before.json) và [danh sách cleanup](RepositoryEvidence/render-cleanup.json). Không gán GUID mới ngẫu nhiên để che reference hỏng.

## Ignore và output

`.gitignore` đã có cache/build/Library/Temp/Logs/UserSettings của Unity project lồng trong HUS_Escape; thêm `.slnx`, local `.vscode`, Python cache và `artifacts/`. Giữ ngoại lệ cho `tools/VT105/Harness.csproj` vì đây là source project thật. Không ignore `.meta`, package manifests hoặc ProjectSettings. Các file cá nhân đang tồn tại vẫn được giữ nguyên trên đĩa.

Chạy lại `python3 tools/Validation/audit_repository.py --output /tmp/hus-assets.json`; tiếp tục native audit qua runner Unity để resolve package GUID. Static audit không thay thế import/compile/build.
