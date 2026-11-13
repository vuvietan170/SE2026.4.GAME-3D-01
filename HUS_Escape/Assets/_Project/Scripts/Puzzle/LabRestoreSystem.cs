using UnityEngine;

namespace HUSEscape.Quests
{
    /// <summary>Scene-local LAB recovery state; reports facts to the existing QuestManager.</summary>
    [DisallowMultipleComponent]
    public sealed class LabRestoreSystem : MonoBehaviour
    {
        [SerializeField] private QuestManager quests;
        [SerializeField] private Renderer screen;
        [SerializeField] private Renderer breakerIndicator;
        [SerializeField] private Renderer[] deviceIndicators = new Renderer[3];
        private MaterialPropertyBlock colors;
        public bool PowerOn { get; private set; }
        public bool InstructionsRead { get; private set; }
        public int ConnectedCount { get; private set; }
        public bool IsReady => PowerOn && ConnectedCount == 3;
        public bool DataAccessed { get; private set; }
        public bool FloorCompleted { get; private set; }
        /// <summary>Applies validated persistence state and refreshes indicators without gameplay events.</summary>
        public void RestoreState(bool power, bool instructions, int connected, bool data, bool complete)
        {
            PowerOn = power; InstructionsRead = instructions; ConnectedCount = connected;
            DataAccessed = data; FloorCompleted = complete; RefreshIndicators();
        }

        private void Awake()
        {
            colors = new MaterialPropertyBlock();
            if (quests == null || screen == null || breakerIndicator == null || deviceIndicators == null || deviceIndicators.Length != 3 ||
                deviceIndicators[0] == null || deviceIndicators[1] == null || deviceIndicators[2] == null)
            { Debug.LogError("LAB recovery requires the existing quest owner and five explicit indicator references.", this); enabled = false; return; }
            RefreshIndicators();
        }

        /// <summary>Handles one physical terminal; actions are ordered and cannot bypass power or instructions.</summary>
        public string Interact(LabInteractionKind kind)
        {
            if (!isActiveAndEnabled) return "Hệ thống LAB chưa được cấu hình đầy đủ.";
            if (!quests.HasObserved("room:LAB")) return "Hãy mở cửa và bước vào LAB trước.";
            if (kind == LabInteractionKind.Computer)
            {
                if (FloorCompleted) return "ĐÃ HOÀN THÀNH NHIỆM VỤ TẦNG 1\n\nNguồn và ba kết nối đều ổn định. Dữ liệu đã được truy cập. Bạn có thể quay lại hành lang qua cửa LAB đã mở; khu vực tiếp theo chưa được triển khai.";
                quests.Signal("lab:inspect");
                if (!PowerOn) return "HỆ THỐNG CHƯA SẴN SÀNG\n\nMáy tính trung tâm đang mất nguồn. Tìm bảng cầu dao trong LAB và nhấn E để khôi phục nguồn.";
                InstructionsRead = true;
                if (!IsReady) return "ĐÃ CÓ NGUỒN — CHƯA KẾT NỐI\n\nDi chuyển tới từng thiết bị và nhấn E theo thứ tự:\nA — nguồn dự phòng\nB — bộ điều khiển\nC — máy chủ dữ liệu\n\nNếu sai thứ tự, các kết nối sẽ được đặt lại để thử từ A. Đã nối: " + ConnectedCount + "/3.";
                DataAccessed = true; quests.Signal("lab:data"); RefreshIndicators();
                return "TRUY CẬP DỮ LIỆU THÀNH CÔNG\n\nBản ghi hướng dẫn: Sau khi hoàn thành kiểm tra LAB, quay lại hành lang tầng 1 qua cửa đã mở. Tìm biển LỐI RA CAMPUS tại cuối hành lang.\n\nSau khi xác nhận đủ 17 mục tiêu, nhấn E tại lối ra để rời tòa nhà T3 và khảo sát khuôn viên. Training Office chưa được mở ở giai đoạn này.\n\nNhấn Xác nhận hoàn thành hoặc Enter để kết thúc chuỗi nhiệm vụ tầng 1.";
            }
            if (!quests.HasObserved("lab:inspect")) return "Hãy kiểm tra trạng thái máy tính trung tâm trước khi thao tác thiết bị.";
            if (kind == LabInteractionKind.Breaker)
            {
                if (PowerOn) return "Cầu dao đã bật. Nguồn điện đang ổn định; không cần bật lại.";
                PowerOn = true; quests.Signal("lab:power"); RefreshIndicators();
                return "ĐÃ KHÔI PHỤC NGUỒN\n\nQuay lại máy tính trung tâm, nhấn E để xem hướng dẫn kết nối ba thiết bị.";
            }
            if (!PowerOn) return "THIẾT BỊ CHƯA CÓ NGUỒN\n\nBật cầu dao LAB trước; thao tác này chưa tạo kết nối nào.";
            if (!InstructionsRead) return "Nguồn đã bật. Hãy quay lại máy tính trung tâm để đọc hướng dẫn kết nối trước.";
            int index = (int)kind - (int)LabInteractionKind.DeviceA;
            if (index < 0 || index > 2) return "Thiết bị không hợp lệ.";
            if (IsReady) return "Cả ba thiết bị đã được kết nối. Quay lại máy tính trung tâm để truy cập dữ liệu.";
            if (index != ConnectedCount)
            {
                ConnectedCount = 0; RefreshIndicators();
                return "SAI THỨ TỰ — ĐÃ ĐẶT LẠI\n\nCác thiết bị trở về chưa kết nối. Bắt đầu lại từ A, sau đó B, rồi C. Nguồn vẫn được giữ, không mất vật phẩm.";
            }
            ConnectedCount++; RefreshIndicators();
            if (IsReady)
            {
                quests.Signal("lab:connected");
                return "KẾT NỐI THÀNH CÔNG — 3/3\n\nMáy tính trung tâm đã sẵn sàng. Quay lại máy tính và nhấn E để truy cập dữ liệu.";
            }
            return "ĐÃ KẾT NỐI THIẾT BỊ " + (char)('A' + index) + " — " + ConnectedCount + "/3\n\nTiếp tục di chuyển tới thiết bị " + (char)('A' + ConnectedCount) + " và nhấn E.";
        }

        /// <summary>Acknowledges the exit guidance; cannot complete the floor before actual data access.</summary>
        public bool ConfirmCompletion()
        {
            if (!isActiveAndEnabled || !DataAccessed || !IsReady || FloorCompleted) return false;
            FloorCompleted = true; quests.Signal("lab:complete"); return true;
        }

        private void RefreshIndicators()
        {
            SetColor(breakerIndicator, PowerOn ? new Color(.15f, .8f, .2f) : new Color(.6f, .12f, .08f));
            SetColor(screen, IsReady ? new Color(.08f, .65f, .25f) : PowerOn ? new Color(.75f, .45f, .05f) : new Color(.07f, .08f, .09f));
            for (int i = 0; i < deviceIndicators.Length; i++)
                SetColor(deviceIndicators[i], i < ConnectedCount ? new Color(.1f, .8f, .25f) : new Color(.22f, .24f, .25f));
        }
        private void SetColor(Renderer target, Color color)
        {
            target.GetPropertyBlock(colors); colors.SetColor("_BaseColor", color); colors.SetColor("_Color", color); target.SetPropertyBlock(colors);
        }
    }
}
