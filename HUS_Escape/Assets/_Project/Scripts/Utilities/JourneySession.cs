using System;
using System.IO;
using System.Linq;
using HUSEscape.Core;
using HUSEscape.Persistence;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HUSEscape.World
{
    public enum JourneyArea { Floor01, Campus, TrainingOffice }
    [Serializable] public sealed class JourneyCheckpoint
    {
        public int version = 1;
        public string scenePath;
        public Floor01SaveData floor;
        public string[] campusFacts;
        public string[] officeFacts;
        public Vector3 position;
        public float yaw;
    }

    /// <summary>Instance-owned travel and one explicit Campus save slot. Only pure checkpoint data survives scene unload.</summary>
    [DisallowMultipleComponent]
    public sealed class JourneySession : MonoBehaviour
    {
        private GameManager manager;
        private SceneLoader loader;
        private JourneySceneEntry current;
        private JourneySceneEntry source;
        private Floor01SaveData floor;
        private JourneyCheckpoint pending;
        private bool fresh;
        private string floorPath, campusPath, officePath;
        private string[] campusMemory = Array.Empty<string>(), officeMemory = Array.Empty<string>();
        public string OfficeSavePath => Path.Combine(StorageRoot, "trainingoffice-v1.json");
        public bool HasOfficeSave => File.Exists(OfficeSavePath);
        public bool IsTrainingOffice => current != null && current.isTrainingOffice;
        public JourneyArea Area => IsTrainingOffice ? JourneyArea.TrainingOffice : IsCampus ? JourneyArea.Campus : JourneyArea.Floor01;
        public string CurrentSaveFile => IsTrainingOffice ? "trainingoffice-v1.json" : IsCampus ? "campus-v1.json" : "floor01-v1.json";
        internal string StorageDirectoryOverride { get; set; }
        private string StorageRoot => StorageDirectoryOverride ?? Application.persistentDataPath;
        public string CampusSavePath => Path.Combine(StorageRoot, "campus-v1.json");
        public string FloorSavePath => current != null && current.floorSave != null ? current.floorSave.SavePath : Path.Combine(StorageRoot, "floor01-v1.json");
        public bool HasCampusSave => File.Exists(CampusSavePath);
        public bool HasFloorSave => File.Exists(FloorSavePath);
        public bool IsCampus => current != null && current.isCampus;
        public bool IsLoading { get; private set; }
        public bool HasCurrentSave => IsTrainingOffice ? HasOfficeSave : IsCampus ? HasCampusSave : HasFloorSave;
        public string LastMessage { get; private set; } = "Lưu thủ công; chuyển Scene không ghi file.";
        public SceneLoader Loader => loader;
        public Floor01SaveData FloorSnapshot => floor;

        internal void Initialize(GameBootstrap core)
        {
            if (loader != null) return; // One subscription pair for this session's lifetime.
            if (FindObjectsByType<JourneySession>(FindObjectsInactive.Include).Any(s => s != this))
                throw new InvalidOperationException("A JourneySession already owns this journey.");
            if (!core.TryGetServices(out var services, out var sceneLoader) || sceneLoader.IsLoading ||
                (services.CurrentState != GameState.Ready && services.CurrentState != GameState.Playing))
                throw new InvalidOperationException("Core services must finish loading before journey initialization.");
            if (services.CurrentState == GameState.Ready && !services.TryChangeState(GameState.Playing))
                throw new InvalidOperationException("Core could not enter Playing.");
            manager = services; loader = sceneLoader;
            loader.SceneLoaded += Loaded; loader.SceneLoadFailed += Failed; DontDestroyOnLoad(gameObject);
        }
        internal void Detach(JourneySceneEntry entry) { if (current == entry) current = null; if (source == entry) source = null; }
        internal void Attach(JourneySceneEntry entry)
        {
            if (IsLoading || loader == null || loader.IsLoading || manager.CurrentState != GameState.Playing) return;
            // Native completion owns binding, not destination Start ordering.
            if (current == entry) return;
            current = entry; floorPath = entry.floorPath; campusPath = entry.campusPath; officePath = entry.officePath; entry.Bind(this);
            if (entry.actorRoot != null) entry.actorRoot.SetActive(true);
            if (StorageDirectoryOverride != null && entry.floorSave != null) entry.floorSave.StorageDirectoryOverride = StorageDirectoryOverride;
            if (entry.isCampus || entry.isTrainingOffice)
            {
                if (floor != null)
                {
                    Place(pending != null ? pending.position : entry.spawn.position, pending != null ? pending.yaw : entry.spawn.eulerAngles.y);
                    entry.inventory.RestoreItems(floor.inventory); entry.quests.RestoreFacts(floor.facts);
                    entry.quests.RestoreCampus(pending?.campusFacts ?? campusMemory);
                }
                else LastMessage = "Campus mở trực tiếp: hãy Continue Campus trong F5 hoặc New Game từ Floor 01.";
                entry.quests.RestoreOffice(pending?.officeFacts ?? officeMemory, entry.isTrainingOffice);
                foreach (var guard in entry.guards) guard.Bind(entry.player.transform);
            }
            else if (!fresh && floor != null) entry.floorSave.RestoreForTravel(floor);
            entry.ui.ProgressRestored(fresh || ((entry.isCampus || entry.isTrainingOffice) && pending == null) || (!entry.isCampus && !entry.isTrainingOffice && floor == null)); pending = null; fresh = false; source = null;
        }
        /// <summary>Requires the real completed floor and an intentional nearby E interaction.</summary>
        public bool TryLeaveFloor()
        {
            if (IsLoading || current == null || current.isCampus || current.isTrainingOffice) return Reject("Chuyển Scene đang bận hoặc không ở Floor 01.");
            if (!current.quests.IsComplete) return Reject("Hoàn thành đủ 17 mục tiêu tầng 1 trước khi ra Campus.");
            try
            {
                var snapshot = current.floorSave.CaptureForTravel();
                if (!snapshot.lab.complete) return Reject("Chưa xác nhận hoàn thành LAB.");
                floor = snapshot; campusMemory = Array.Empty<string>(); officeMemory = Array.Empty<string>(); pending = null; return Begin(campusPath);
            }
            catch (Exception e) { return Reject(e.Message); }
        }
        /// <summary>Enters only after the three Campus discoveries; never writes save files.</summary>
        public bool TryEnterOffice()
        {
            if (IsLoading || !IsCampus) return Reject("Hãy đến cửa Phòng Đào tạo từ Campus.");
            if (current.quests.CampusIndex < 3) return Reject("Khảo sát sân trường, đọc sơ đồ Campus và biển hướng Phòng Đào tạo trước khi vào.");
            if (floor == null) return Reject("Hãy Continue Campus trong F5 hoặc tiếp tục hành trình từ Floor 01.");
            campusMemory = current.quests.CaptureCampusFacts(); pending = null;
            return Begin(officePath);
        }
        /// <summary>Returns to the office entrance in Campus, retaining facts without an automatic save.</summary>
        public bool TryReturnCampus()
        {
            if (IsLoading || !IsTrainingOffice || floor == null) return Reject("Chưa thể trở về Campus.");
            officeMemory = current.quests.CaptureOfficeFacts();
            pending = new JourneyCheckpoint { scenePath = campusPath, position = new Vector3(16,1.08f,51), yaw = 180, campusFacts = campusMemory, officeFacts = officeMemory };
            return Begin(campusPath);
        }
        private bool Begin(string path)
        {
            if (IsLoading || current == null || loader.IsLoading) return Reject("Đang tải Scene; yêu cầu lặp bị từ chối.");
            source = current; IsLoading = true; source.Gate.Block();
            if (!loader.TryLoadScene(path, GameState.Playing, out string error))
            { IsLoading = false; if (source != null) source.ui.CloseModal(); source = null; pending = null; return Reject(error); }
            LastMessage = "Đang chuyển khu vực…"; return true;
        }
        private void Loaded(string path)
        {
            if (!IsLoading) return;
            IsLoading = false; current = null;
            var entries = FindObjectsByType<JourneySceneEntry>().Where(e => e.gameObject.scene.path == path).ToArray();
            if (entries.Length != 1) { Reject("Scene đích thiếu liên kết hành trình. Không ghi đè save."); return; }
            try { Attach(entries[0]); LastMessage = "Đã chuyển khu vực. Tiến trình chưa tự động ghi vào đĩa."; }
            catch (Exception e) { Reject("Không thể khôi phục khu vực: " + e.Message); Debug.LogException(e); }
        }
        private void Failed(string path, string error)
        {
            if (!IsLoading) return;
            IsLoading = false; pending = null;
            if (source != null) { current = source; source.ui.CloseModal(); }
            source = null; Reject(error);
        }
        private bool Reject(string message) { LastMessage = message; return false; }
        /// <summary>Current context uses the original Floor V1 slot or the explicit Campus slot.</summary>
        public bool TrySave(bool overwrite = false)
        {
            if (IsLoading || current == null) return Reject("Đang tải khu vực.");
            if (!IsCampus && !IsTrainingOffice) { bool result = current.floorSave.TrySave(overwrite); LastMessage = current.floorSave.LastMessage; return result; }
            try
            {
                if (!current.player.isGrounded || !Safe(current.player.transform.position)) return Reject("Hãy đứng trên nền thoáng trước khi lưu.");
                string slot = IsTrainingOffice ? OfficeSavePath : CampusSavePath;
                if (File.Exists(slot) && !overwrite) return Reject("Cần xác nhận ghi đè slot khu vực hiện tại.");
                var data = new JourneyCheckpoint { scenePath = IsTrainingOffice ? officePath : campusPath, floor = floor, campusFacts = current.quests.CaptureCampusFacts(), officeFacts = IsTrainingOffice ? current.quests.CaptureOfficeFacts() : officeMemory, position = current.player.transform.position, yaw = current.player.transform.eulerAngles.y };
                Validate(data, IsTrainingOffice); Directory.CreateDirectory(StorageRoot);
                string staging = slot + ".pending";
                File.WriteAllText(staging, JsonUtility.ToJson(data, true), new System.Text.UTF8Encoding(false));
                if (File.Exists(slot)) File.Replace(staging, slot, slot + ".previous"); else File.Move(staging, slot);
                LastMessage = "Đã lưu khu vực hiện tại; các slot khác được giữ nguyên."; return true;
            }
            catch (Exception e) { return Reject("Không thể lưu Campus: " + e.Message); }
        }
        private void Validate(JourneyCheckpoint data, bool office = false)
        {
            if (data == null || data.version != 1 || data.scenePath != (office ? officePath : campusPath) || data.floor == null || !data.floor.lab.complete || data.campusFacts == null ||
                data.campusFacts.Distinct().Count() != data.campusFacts.Length || data.campusFacts.Any(f => f != "campus:courtyard" && f != "campus:map" && f != "campus:direction") ||
                !Finite(data.position.x) || !Finite(data.position.y) || !Finite(data.position.z) || !Finite(data.yaw) || data.position.x < (office ? -8 : -28) || data.position.x > (office ? 8 : 28) || data.position.z < 0 || data.position.z > (office ? 24 : 62) || data.position.y < .5f || data.position.y > 5)
                throw new InvalidDataException("Save Campus hỏng, sai phiên bản, Scene hoặc trạng thái.");
            if ((office && (data.campusFacts.Length != 3 || data.officeFacts == null)) || (data.officeFacts != null && (data.officeFacts.Distinct().Count() != data.officeFacts.Length ||
                data.officeFacts.Any(f => f != "office:notice" && f != "office:document" && f != "office:computer") ||
                data.officeFacts.Contains("office:computer") && (!data.officeFacts.Contains("office:notice") || !data.officeFacts.Contains("office:document")))))
                throw new InvalidDataException("Invalid Training Office checkpoint.");
            ValidateFloor(data.floor);
        }
        private void ValidateFloor(Floor01SaveData data) => Floor01SaveSystem.ValidateSnapshot(data, current.quests.Definitions, new[] { "Key_A101", "Key_A102", "Key_Lab" }, current.inventory.Capacity);
        private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        /// <summary>Loads an explicitly chosen slot; validation happens before any scene change.</summary>
        public bool TryContinue(bool campus) => TryContinue(campus ? JourneyArea.Campus : JourneyArea.Floor01);
        public bool TryContinue(JourneyArea area)
        {
            bool campus = area == JourneyArea.Campus, office = area == JourneyArea.TrainingOffice;
            if (IsLoading || current == null) return Reject("Đang chuyển khu vực.");
            try
            {
                string path = office ? OfficeSavePath : campus ? CampusSavePath : FloorSavePath;
                if (!File.Exists(path)) return Reject("Chưa có bản lưu cho khu vực đã chọn.");
                if (new FileInfo(path).Length > 1024 * 1024) return Reject("File lưu quá lớn.");
                string json = File.ReadAllText(path, new System.Text.UTF8Encoding(false, true));
                if (campus || office)
                {
                    var data = JsonUtility.FromJson<JourneyCheckpoint>(json); Validate(data, office); floor = data.floor; pending = data; campusMemory = data.campusFacts; officeMemory = data.officeFacts ?? Array.Empty<string>();
                    if (Area == area) { var entry = current; current = null; Attach(entry); LastMessage = "Đã Continue Campus, không phát lại phần thưởng."; return true; }
                    return Begin(office ? officePath : campusPath);
                }
                var legacy = JsonUtility.FromJson<Floor01SaveData>(json); ValidateFloor(legacy);
                if (!IsCampus && !IsTrainingOffice) { bool result = current.floorSave.TryContinue(); LastMessage = current.floorSave.LastMessage; return result; }
                floor = legacy; campusMemory = Array.Empty<string>(); officeMemory = Array.Empty<string>(); pending = null; return Begin(floorPath);
            }
            catch (Exception e) { return Reject("Không thể Continue: " + e.Message + " Không ghi đè file lưu."); }
        }
        public bool TryNewGame(bool confirmed)
        {
            if (!confirmed || IsLoading || current == null) return Reject("Cần xác nhận New Game khi không tải Scene.");
            if (!IsCampus && !IsTrainingOffice) { bool result = current.floorSave.TryNewGame(true); LastMessage = current.floorSave.LastMessage; if (result) floor = null; return result; }
            var previous = floor; fresh = true; floor = null; pending = null; campusMemory = Array.Empty<string>(); officeMemory = Array.Empty<string>();
            if (Begin(floorPath)) return true;
            floor = previous; fresh = false; return false;
        }
        private bool Safe(Vector3 p)
        {
            var cc = current.player; float r = cc.radius - .03f; Vector3 c = p + cc.center, offset = Vector3.up * (cc.height * .5f - cc.radius);
            return !Physics.OverlapCapsule(c - offset, c + offset, r, ~0, QueryTriggerInteraction.Ignore).Any(x => !x.transform.IsChildOf(cc.transform)) &&
                Physics.SphereCast(c - offset, r, Vector3.down, out _, .3f, ~0, QueryTriggerInteraction.Ignore);
        }
        private void Place(Vector3 position, float yaw)
        {
            var cc = current.player; cc.enabled = false;
            try { Physics.SyncTransforms(); if (!Safe(position)) { position = current.spawn.position; yaw = current.spawn.eulerAngles.y; } if (!Safe(position)) throw new InvalidDataException("Không có vị trí đứng an toàn."); cc.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0)); Physics.SyncTransforms(); }
            finally { cc.enabled = true; }
        }
        private void OnGUI()
        {
            if (!IsLoading) return;
            GUI.Box(new Rect(Screen.width / 2f - 200, Screen.height / 2f - 40, 400, 80), "Đang tải Campus / khu vực đã chọn…");
        }
        private void OnDestroy() { if (loader != null) { loader.SceneLoaded -= Loaded; loader.SceneLoadFailed -= Failed; } }
    }
}
