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

    /// <summary>Commits checkpoint data only after its destination has restored successfully.</summary>
    [DisallowMultipleComponent]
    public sealed class JourneySession : MonoBehaviour
    {
        private sealed class Proposal
        {
            internal string Path;
            internal Floor01SaveData Floor;
            internal string[] Campus = Array.Empty<string>(), Office = Array.Empty<string>();
            internal JourneyCheckpoint Pose;
            internal bool Fresh;
        }
        private sealed class Travel
        {
            internal Proposal Proposed, Before;
            internal JourneySceneEntry Source;
            internal bool OwnsGate;
            internal HUSEscape.Player.GameplayInputGate.Snapshot InputBefore;
        }
        private GameBootstrap bootstrap;
        private GameManager manager;
        private SceneLoader loader;
        private JourneySceneEntry current;
        private Floor01SaveData floor;
        private Travel travel;
        private bool applying;
        private Proposal recovery;
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
        public bool IsLoading => travel != null || applying;
        public bool RecoveryRequired { get; private set; }
        public string RecoveryScenePath => recovery?.Path;
        public bool HasCurrentSave => IsTrainingOffice ? HasOfficeSave : IsCampus ? HasCampusSave : HasFloorSave;
        public string LastMessage { get; private set; } = "Lưu thủ công; chuyển Scene không ghi file.";
        public SceneLoader Loader => loader;
        // Give callers data, never a mutable reference to committed state.
        public Floor01SaveData FloorSnapshot => Copy(floor);
        private static T Copy<T>(T data) where T : class => data == null ? null : JsonUtility.FromJson<T>(JsonUtility.ToJson(data));

        internal void Initialize(GameBootstrap core)
        {
            if (loader != null) return;
            if (FindObjectsByType<JourneySession>(FindObjectsInactive.Include).Any(s => s != this))
                throw new InvalidOperationException("A JourneySession already owns this journey.");
            if (!core.TryGetServices(out var services, out var sceneLoader) || sceneLoader.IsLoading ||
                (services.CurrentState != GameState.Ready && services.CurrentState != GameState.Playing))
                throw new InvalidOperationException("Core services must finish loading before journey initialization.");
            if (services.CurrentState == GameState.Ready && !services.TryChangeState(GameState.Playing))
                throw new InvalidOperationException("Core could not enter Playing.");
            bootstrap = core; manager = services; loader = sceneLoader;
            loader.SceneLoaded += Loaded; loader.SceneLoadFailed += Failed; DontDestroyOnLoad(gameObject);
        }
        internal void Detach(JourneySceneEntry entry) { if (current == entry) current = null; }
        private Proposal LiveSnapshot()
        {
            if (current == null) return recovery;
            return new Proposal {
                Path = current.gameObject.scene.path,
                Floor = current.floorSave != null ? current.floorSave.CaptureForRestore() : Copy(floor),
                Campus = current.isCampus || current.isTrainingOffice ? current.quests.CaptureCampusFacts() : campusMemory.ToArray(),
                Office = current.isTrainingOffice ? current.quests.CaptureOfficeFacts() : officeMemory.ToArray(),
                Pose = new JourneyCheckpoint { position = current.player.transform.position, yaw = current.player.transform.eulerAngles.y }
            };
        }
        private static void CheckEntry(JourneySceneEntry entry)
        {
            if (entry == null || !entry.isActiveAndEnabled || entry.player == null || entry.inventory == null || entry.quests == null || entry.ui == null ||
                entry.spawn == null || entry.viewCamera == null || entry.Gate == null || !entry.ui.CanBindJourney(entry.quests, entry.Gate) || entry.guards == null || entry.guards.Any(g => g == null) ||
                (!entry.isCampus && !entry.isTrainingOffice && entry.floorSave == null) || (entry.isCampus && entry.isTrainingOffice))
                throw new InvalidOperationException("Scene đích thiếu liên kết hành trình.");
        }
        internal void Attach(JourneySceneEntry entry)
        {
            if (IsLoading || RecoveryRequired || loader == null || loader.IsLoading || manager.CurrentState != GameState.Playing || current == entry) return;
            try
            {
                ApplyAndCommit(entry, new Proposal { Path = entry.gameObject.scene.path, Floor = Copy(floor), Campus = campusMemory.ToArray(), Office = officeMemory.ToArray() });
            }
            catch (Exception error)
            {
                recovery = new Proposal { Path = entry.gameObject.scene.path, Floor = Copy(floor), Campus = campusMemory.ToArray(), Office = officeMemory.ToArray() };
                EnterRecovery(error.Message, entry);
            }
        }
        private void ApplyAndCommit(JourneySceneEntry entry, Proposal proposed)
        {
            if (applying) throw new InvalidOperationException("Restore is already in progress.");
            applying = true;
            try
            {
                CheckEntry(entry);
                if (proposed.Floor != null)
                    Floor01SaveSystem.ValidateSnapshot(proposed.Floor, entry.quests.Definitions, new[] { "Key_A101", "Key_A102", "Key_Lab" }, entry.inventory.Capacity);
                if (entry.actorRoot != null) entry.actorRoot.SetActive(true); // Awake prepares actor-owned state.
                entry.Gate.Bind(bootstrap);
                if (current != entry) entry.Gate.HoldForTravel();
                if (StorageDirectoryOverride != null && entry.floorSave != null) entry.floorSave.StorageDirectoryOverride = StorageDirectoryOverride;
                // World-area restore has no puzzle side effects, and resolves pose before touching facts.
                if (entry.isCampus || entry.isTrainingOffice)
                {
                    var position = proposed.Pose != null ? proposed.Pose.position : entry.spawn.position;
                    float yaw = proposed.Pose != null ? proposed.Pose.yaw : entry.spawn.eulerAngles.y;
                    Place(entry, position, yaw);
                    if (proposed.Floor != null) { entry.inventory.RestoreItems(proposed.Floor.inventory); entry.quests.RestoreFacts(proposed.Floor.facts); }
                    entry.quests.RestoreAreas(proposed.Campus, proposed.Floor != null, proposed.Office, entry.isTrainingOffice);
                    foreach (var guard in entry.guards) guard.Bind(entry.player.transform);
                }
                else if (proposed.Fresh) entry.floorSave.RestoreFreshForTravel();
                else if (proposed.Floor != null) entry.floorSave.RestoreForTravel(proposed.Floor);
                // Everything that can reject the checkpoint ran before these committed fields change.
                floor = Copy(proposed.Floor); campusMemory = proposed.Campus.ToArray(); officeMemory = proposed.Office.ToArray();
                current = entry; floorPath = entry.floorPath; campusPath = entry.campusPath; officePath = entry.officePath;
                recovery = null; RecoveryRequired = false;
                entry.Bind(this);
                // Floor restore already notified its UI through Restored. Calling it
                // again would replay an objective notice when Continue crosses scenes.
                bool world = entry.isCampus || entry.isTrainingOffice;
                if (world && proposed.Floor == null) LastMessage = "Khu vực mở trực tiếp: hãy Continue trong F5 hoặc New Game từ Floor 01.";
                if (world || (!proposed.Fresh && proposed.Floor == null))
                    entry.ui.ProgressRestored((world && proposed.Pose == null) || (!world && proposed.Floor == null));
                entry.Gate.ReleaseAfterFrame();
            }
            finally { applying = false; }
        }
        public bool TryLeaveFloor()
        {
            if (IsLoading || RecoveryRequired || current == null || current.isCampus || current.isTrainingOffice) return Reject("Không thể rời Floor 01 lúc này.");
            if (!current.quests.IsComplete) return Reject("Hoàn thành đủ 17 mục tiêu tầng 1 trước khi ra Campus.");
            try
            {
                var snapshot = current.floorSave.CaptureForTravel();
                if (!snapshot.lab.complete) return Reject("Chưa xác nhận hoàn thành LAB.");
                return Begin(new Proposal { Path = campusPath, Floor = snapshot });
            }
            catch (Exception error) { return Reject(error.Message); }
        }
        public bool TryEnterOffice()
        {
            if (IsLoading || RecoveryRequired || !IsCampus || floor == null) return Reject("Hãy đến Phòng Đào tạo từ một hành trình Campus hợp lệ.");
            if (current.quests.CampusIndex < 3) return Reject("Khảo sát sân trường, sơ đồ và biển hướng trước khi vào.");
            return Begin(new Proposal { Path = officePath, Floor = Copy(floor), Campus = current.quests.CaptureCampusFacts(), Office = officeMemory.ToArray() });
        }
        public bool TryReturnCampus()
        {
            if (IsLoading || RecoveryRequired || !IsTrainingOffice || floor == null) return Reject("Chưa thể trở về Campus.");
            return Begin(new Proposal { Path = campusPath, Floor = Copy(floor), Campus = current.quests.CaptureCampusFacts(), Office = current.quests.CaptureOfficeFacts(),
                Pose = new JourneyCheckpoint { position = new Vector3(16, 1.08f, 51), yaw = 180 } });
        }
        private bool Begin(Proposal proposed)
        {
            if (IsLoading || loader == null || loader.IsLoading || (current == null && !RecoveryRequired)) return Reject("Đang tải Scene hoặc không có phiên để chuyển.");
            if (current != null && current.Gate.IsBlocked && current.ui.Mode == HUSEscape.Quests.QuestModal.None)
                return Reject("Hãy chờ input được mở lại trước khi chuyển khu vực.");
            Travel accepted;
            try
            {
                accepted = new Travel { Proposed = proposed, Before = LiveSnapshot(), Source = current, OwnsGate = current != null && !current.Gate.IsBlocked };
                if (current != null) accepted.InputBefore = current.Gate.CaptureSnapshot();
                if (accepted.Before == null) return Reject("Không có checkpoint phục hồi.");
            }
            catch (Exception captureError) { return Reject("Không thể chụp phiên hiện tại: " + captureError.Message); }
            travel = accepted;
            if (accepted.Source != null) accepted.Source.Gate.Acquire(this);
            if (!loader.TryLoadScene(proposed.Path, GameState.Playing, out string error))
            {
                // A synchronous backend failure may already have called Failed.
                if (ReferenceEquals(travel, accepted)) Abort(accepted, error);
                return false;
            }
            if (ReferenceEquals(travel, accepted)) LastMessage = "Đang chuyển khu vực…";
            return !RecoveryRequired || IsLoading;
        }
        private void Update()
        {
            // Closing a source modal cannot release the travel pause while native loading is pending.
            if (travel != null && travel.Source != null) travel.Source.Gate.Acquire(this);
        }
        private void Loaded(string path)
        {
            var accepted = travel;
            if (accepted == null || path != accepted.Proposed.Path) return;
            var entries = FindObjectsByType<JourneySceneEntry>().Where(e => e.gameObject.scene.path == path).ToArray();
            JourneySceneEntry target = entries.Length == 1 ? entries[0] : null;
            // A surviving source (test/additive backend) must release its global pause before destination Awake.
            if (accepted.Source != null) accepted.Source.Gate.ReleaseImmediately();
            try
            {
                if (target == null) throw new InvalidOperationException("Scene đích thiếu hoặc trùng JourneySceneEntry.");
                ApplyAndCommit(target, accepted.Proposed);
                travel = null;
                if (accepted.Source != null && accepted.Source != target) accepted.Source.ui.CloseModal();
                LastMessage = "Đã chuyển khu vực. Tiến trình chưa tự động ghi vào đĩa.";
            }
            catch (Exception error) { Abort(accepted, "Không thể khôi phục khu vực: " + error.Message, target); }
        }
        private void Failed(string path, string error)
        {
            var accepted = travel;
            if (accepted != null && path == accepted.Proposed.Path) Abort(accepted, error);
        }
        private void Abort(Travel accepted, string error, JourneySceneEntry target = null)
        {
            if (!ReferenceEquals(travel, accepted)) return;
            travel = null;
            if (accepted.Source != null && accepted.Source.gameObject.scene.IsValid() && accepted.Source.gameObject.scene.isLoaded &&
                (accepted.Source.floorSave == null || !accepted.Source.floorSave.RecoveryRequired))
            {
                current = accepted.Source;
                if (target != null && target != current) Quarantine(target);
                current.Gate.RestoreSnapshot(accepted.InputBefore);
                if (accepted.OwnsGate) current.Gate.HoldForTravel();
                if (accepted.OwnsGate || current.ui.Mode == HUSEscape.Quests.QuestModal.None) current.Gate.ReleaseAfterFrame();
                // Keep an open modal's lease; if the user closed it during loading,
                // finish that close instead of restoring an invisible permanent pause.
                Reject(error + " Phiên trước được giữ nguyên.");
            }
            else { recovery = accepted.Before; EnterRecovery(error, target); }
        }
        private void EnterRecovery(string error, JourneySceneEntry target)
        {
            current = null; RecoveryRequired = true;
            Quarantine(target);
            Time.timeScale = 1; Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            Reject(error + " Scene trước không còn khả dụng. Chọn tải lại checkpoint phiên trước để phục hồi; không có rollback scene.");
        }
        private static void Quarantine(JourneySceneEntry target)
        {
            if (target == null) return;
            target.StopAllCoroutines(); target.enabled = false;
            if (target.player != null && target.Gate != null) target.Gate.ReleaseImmediately();
            if (target.ui != null) target.ui.enabled = false;
            if (target.actorRoot != null) target.actorRoot.SetActive(false);
            else
            {
                if (target.player != null) target.player.gameObject.SetActive(false);
                if (target.viewCamera != null) target.viewCamera.gameObject.SetActive(false);
            }
        }
        public bool TryRecover()
        {
            if (!RecoveryRequired || recovery == null) return Reject("Không có phiên cần phục hồi.");
            return Begin(recovery);
        }
        private bool Reject(string message) { LastMessage = message; return false; }
        /// <summary>Current context uses the original Floor V1 slot or the explicit Campus slot.</summary>
        public bool TrySave(bool overwrite = false)
        {
            if (IsLoading || RecoveryRequired || current == null) return Reject("Đang tải khu vực.");
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
            if (data == null || data.version != 1 || data.scenePath != (office ? officePath : campusPath) || data.floor == null || data.floor.lab == null || !data.floor.lab.complete || data.campusFacts == null ||
                data.campusFacts.Distinct().Count() != data.campusFacts.Length || data.campusFacts.Any(f => f != "campus:courtyard" && f != "campus:map" && f != "campus:direction") ||
                !Finite(data.position.x) || !Finite(data.position.y) || !Finite(data.position.z) || !Finite(data.yaw) || data.position.x < (office ? -8 : -28) || data.position.x > (office ? 8 : 28) || data.position.z < 0 || data.position.z > (office ? 24 : 62) || data.position.y < .5f || data.position.y > 5)
                throw new InvalidDataException("Save Campus hỏng, sai phiên bản, Scene hoặc trạng thái.");
            if ((office && (data.campusFacts.Length != 3 || data.officeFacts == null)) || (data.officeFacts != null && ((data.officeFacts.Length > 0 && data.campusFacts.Length != 3) || data.officeFacts.Distinct().Count() != data.officeFacts.Length ||
                data.officeFacts.Any(f => f != "office:notice" && f != "office:document" && f != "office:computer") ||
                data.officeFacts.Contains("office:computer") && (!data.officeFacts.Contains("office:notice") || !data.officeFacts.Contains("office:document")))))
                throw new InvalidDataException("Invalid Training Office checkpoint.");
            ValidateFloor(data.floor);
        }
        private void ValidateFloor(Floor01SaveData data) => Floor01SaveSystem.ValidateSnapshot(data, current.quests.Definitions, new[] { "Key_A101", "Key_A102", "Key_Lab" }, current.inventory.Capacity);
        private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        public bool TryContinue(bool campus) => TryContinue(campus ? JourneyArea.Campus : JourneyArea.Floor01);
        public bool TryContinue(JourneyArea area)
        {
            if (!Enum.IsDefined(typeof(JourneyArea), area) || IsLoading || RecoveryRequired || current == null || loader.IsLoading)
                return Reject("Đang chuyển khu vực hoặc khu vực không hợp lệ.");
            bool campus = area == JourneyArea.Campus, office = area == JourneyArea.TrainingOffice;
            try
            {
                string path = office ? OfficeSavePath : campus ? CampusSavePath : FloorSavePath;
                if (!File.Exists(path)) return Reject("Chưa có bản lưu cho khu vực đã chọn.");
                if (new FileInfo(path).Length > 1024 * 1024) return Reject("File lưu quá lớn.");
                string json = File.ReadAllText(path, new System.Text.UTF8Encoding(false, true));
                Proposal proposed;
                if (campus || office)
                {
                    var data = SaveJson.Read<JourneyCheckpoint>(json, campus); Validate(data, office);
                    proposed = new Proposal { Path = office ? officePath : campusPath, Floor = data.floor, Campus = data.campusFacts,
                        Office = data.officeFacts ?? Array.Empty<string>(), Pose = data };
                }
                else
                {
                    var data = SaveJson.Read<Floor01SaveData>(json); ValidateFloor(data);
                    if (Area == JourneyArea.Floor01)
                    {
                        var before = LiveSnapshot();
                        applying = true;
                        try
                        {
                            bool result = current.floorSave.TryRestoreCheckpoint(data); LastMessage = current.floorSave.LastMessage;
                            if (!result && current.floorSave.RecoveryRequired) { recovery = before; EnterRecovery(LastMessage, current); }
                            if (result) { floor = Copy(data); campusMemory = Array.Empty<string>(); officeMemory = Array.Empty<string>(); }
                            return result;
                        }
                        finally { applying = false; }
                    }
                    proposed = new Proposal { Path = floorPath, Floor = data };
                }
                if (Area != area) return Begin(proposed);
                // Same-scene Continue uses the same apply-before-commit rule. World pose
                // is checked before inventory/facts, so a rejected position changes nothing.
                ApplyAndCommit(current, proposed);
                LastMessage = "Đã Continue; không phát lại phần thưởng."; return true;
            }
            catch (Exception error)
            {
                if (current != null && current.floorSave != null && current.floorSave.RecoveryRequired)
                { recovery = LiveSnapshot(); EnterRecovery(error.Message, current); }
                return Reject("Không thể Continue: " + error.Message + " Không ghi đè file lưu.");
            }
        }
        public bool TryNewGame(bool confirmed)
        {
            if (!confirmed || IsLoading || RecoveryRequired || current == null || loader.IsLoading) return Reject("Cần xác nhận New Game khi không tải Scene.");
            if (!IsCampus && !IsTrainingOffice)
            {
                var before = LiveSnapshot();
                applying = true;
                try
                {
                    bool result = current.floorSave.TryNewGame(true); LastMessage = current.floorSave.LastMessage;
                    if (!result && current.floorSave.RecoveryRequired) { recovery = before; EnterRecovery(LastMessage, current); }
                    if (result) { floor = null; campusMemory = Array.Empty<string>(); officeMemory = Array.Empty<string>(); }
                    return result;
                }
                finally { applying = false; }
            }
            return Begin(new Proposal { Path = floorPath, Fresh = true });
        }
        private static bool Safe(JourneySceneEntry entry, Vector3 position)
        {
            var cc = entry.player; float r = cc.radius - .03f; Vector3 c = position + cc.center, offset = Vector3.up * (cc.height * .5f - cc.radius);
            return !Physics.OverlapCapsule(c - offset, c + offset, r, ~0, QueryTriggerInteraction.Ignore).Any(x => !x.transform.IsChildOf(cc.transform)) &&
                Physics.SphereCast(c - offset, r, Vector3.down, out _, .3f, ~0, QueryTriggerInteraction.Ignore);
        }
        private bool Safe(Vector3 position) => Safe(current, position);
        private static void Place(JourneySceneEntry entry, Vector3 position, float yaw)
        {
            var cc = entry.player; bool enabled = cc.enabled; cc.enabled = false;
            try
            {
                Physics.SyncTransforms();
                if (!Safe(entry, position)) { position = entry.spawn.position; yaw = entry.spawn.eulerAngles.y; }
                if (!Safe(entry, position)) throw new InvalidDataException("Không có vị trí đứng an toàn.");
                cc.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0)); Physics.SyncTransforms();
            }
            finally { cc.enabled = enabled; }
        }
        private void OnGUI()
        {
            if (RecoveryRequired)
            {
                GUI.Box(new Rect(20, 20, Screen.width - 40, 140), LastMessage);
                GUI.enabled = !IsLoading;
                if (GUI.Button(new Rect(40, 105, 320, 40), "Tải lại checkpoint phiên trước")) TryRecover();
                GUI.enabled = true;
            }
            else if (IsLoading) GUI.Box(new Rect(Screen.width / 2f - 200, Screen.height / 2f - 40, 400, 80), "Đang tải khu vực…");
        }
        private void OnDestroy() { if (loader != null) { loader.SceneLoaded -= Loaded; loader.SceneLoadFailed -= Failed; } }
    }
}
