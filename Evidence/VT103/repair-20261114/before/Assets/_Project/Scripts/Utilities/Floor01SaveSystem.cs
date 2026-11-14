using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HUSEscape.Inventory;
using HUSEscape.Quests;
using UnityEngine;

namespace HUSEscape.Persistence
{
    [Serializable] public sealed class SavedDoor { public string keyId; public bool locked; public bool open; }
    [Serializable] public sealed class SavedLab { public bool power, instructions, data, complete; public int connected; }
    [Serializable] public sealed class Floor01SaveData
    {
        public int version;
        public string scene, savedUtc;
        public string[] questIds, facts, inventory;
        public SavedDoor[] doors;
        public bool cabinetUnlocked, sequenceSolved;
        public SavedLab lab;
        public Vector3 playerPosition;
        public float playerYaw;
    }

    /// <summary>One explicit floor checkpoint, with validated, silent restoration of existing systems.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(-8000)]
    public sealed class Floor01SaveSystem : MonoBehaviour
    {
        [SerializeField] private QuestManager quests;
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private CharacterController player;
        [SerializeField] private DoorInteraction[] doors;
        [SerializeField] private PickupItem[] pickups;
        [SerializeField] private CodeLockCabinet cabinet;
        [SerializeField] private SequenceLockPanel sequence;
        [SerializeField] private LabRestoreSystem lab;
        private Vector3 spawn;
        private float spawnYaw;
        private bool restoring;
        internal string StorageDirectoryOverride { get; set; }
        public string SavePath => Path.Combine(StorageDirectoryOverride ?? Application.persistentDataPath, "floor01-v1.json");
        public bool HasSaveFile => File.Exists(SavePath);
        public string LastMessage { get; private set; } = "Chưa tải hoặc ghi tiến trình trong phiên này.";
        public bool LastLoadUsedFallback { get; private set; }
        public int RestoreCount { get; private set; }
        public bool RecoveryRequired { get; private set; }
        public event Action<bool> Restored;
        /// <summary>True only while a validated snapshot is being applied or rolled back.</summary>
        public bool IsRestoring => restoring;
        private void Awake()
        {
            spawn = player.transform.position;
            spawnYaw = player.transform.eulerAngles.y;

            // Keep autosave scene-local; it never owns or replaces the manual save slot.
            var autosave = GetComponent<Floor01Autosave>() ?? gameObject.AddComponent<Floor01Autosave>();
            autosave.Bind(this, quests);
        }

        private bool Stable()
        {
            if (restoring || RecoveryRequired) { LastMessage = "Đang khôi phục hoặc cần tải lại khu vực."; return false; }
            if (quests == null || inventory == null || player == null || cabinet == null || sequence == null || lab == null ||
                doors == null || doors.Length != 3 || pickups == null || pickups.Length != 3 || doors.Any(d => d == null) || pickups.Any(p => p == null))
                throw new InvalidOperationException("Liên kết Save/Load tầng 1 chưa đầy đủ.");
            if (!player.isGrounded || cabinet.IsOpening || sequence.IsOpening || pickups.Any(p => p.IsBusy) || doors.Any(d => d.IsBusy ||
                (d.TryGetComponent<DoorHandleIK>(out var ik) && (ik.IsPrepared || ik.CurrentWeight > .001f))))
            { LastMessage = "Hãy đứng trên sàn và chờ cửa/nhặt đồ/IK hoàn tất. Đóng bảng này nếu cần tiếp tục animation."; return false; }
            return true;
        }

        /// <summary>Saves explicitly. An existing file requires a separate overwrite confirmation.</summary>
        public bool TrySave(bool confirmOverwrite = false)
        {
            try
            {
                if (!Stable()) return false;
                if (HasSaveFile && !confirmOverwrite) { LastMessage = "Đã có bản lưu. Cần xác nhận ghi đè; Continue không ghi dữ liệu."; return false; }
                var data = Capture(); Validate(data);
                if (!SafePosition(data.playerPosition)) { LastMessage = "Vị trí hiện tại không đủ an toàn để lưu. Hãy ra lối đi thoáng."; return false; }
                string directory = Path.GetDirectoryName(SavePath); Directory.CreateDirectory(directory);
                string pending = SavePath + ".pending";
                File.WriteAllText(pending, JsonUtility.ToJson(data, true), new System.Text.UTF8Encoding(false));
                if (File.Exists(SavePath)) File.Replace(pending, SavePath, SavePath + ".previous");
                else File.Move(pending, SavePath);
                LastMessage = "Đã lưu tiến trình. Bản cũ được giữ ở .previous khi ghi đè."; return true;
            }
            catch (Exception e) { LastMessage = "Không thể lưu: " + e.Message; return false; }
        }

        /// <summary>Validates the entire file before altering gameplay; errors leave the current session intact.</summary>
        public bool TryContinue()
        {
            try
            {
                if (!Stable()) return false;
                if (!HasSaveFile) { LastMessage = "Chưa có bản lưu. Bạn có thể chọn New Game; không có dữ liệu nào bị ghi đè."; return false; }
                if (new FileInfo(SavePath).Length > 1024 * 1024) throw new InvalidDataException("Bản lưu quá lớn.");
                var data = SaveJson.Read<Floor01SaveData>(File.ReadAllText(SavePath, new System.Text.UTF8Encoding(false, true)));
                Validate(data); RestoreWithRollback(data, false);
                LastMessage = LastLoadUsedFallback ? "Đã tải tiến trình; vị trí cũ không an toàn nên Player trở về điểm xuất phát." : "Đã tải tiến trình; không phát lại phần thưởng hoặc animation.";
                return true;
            }
            catch (Exception e) { LastMessage = "Không thể Continue: " + e.Message + (RecoveryRequired ? " Cần tải lại khu vực để phục hồi." : " Phiên hiện tại và file lưu được giữ nguyên."); return false; }
        }

        /// <summary>Starts a fresh session only after confirmation; never deletes or overwrites a save.</summary>
        public bool TryNewGame(bool confirmed)
        {
            if (!confirmed) { LastMessage = "Xác nhận New Game để đặt lại phiên chơi. Bản lưu trên đĩa không bị xóa."; return false; }
            try
            {
                if (!Stable()) return false;
                RestoreFreshForTravel(); LastMessage = "New Game đã bắt đầu. Bản lưu cũ vẫn có thể Continue; lưu đè cần xác nhận riêng."; return true;
            }
            catch (Exception e) { LastMessage = "Không thể bắt đầu phiên mới: " + e.Message; return false; }
        }

        internal bool TryRestoreCheckpoint(Floor01SaveData data)
        {
            try
            {
                if (!Stable()) return false;
                Validate(data); RestoreWithRollback(data, false);
                LastMessage = "Đã khôi phục checkpoint; không phát lại phần thưởng."; return true;
            }
            catch (Exception error) { LastMessage = "Không thể khôi phục checkpoint: " + error.Message; return false; }
        }

        internal void RestoreFreshForTravel()
        {
            var data = new Floor01SaveData { version = 1, scene = "Floor01_Prototype", questIds = quests.Definitions.Select(q => q.Id).ToArray(),
                facts = Array.Empty<string>(), inventory = Array.Empty<string>(), doors = doors.Select(d => new SavedDoor { keyId = d.RequiredKeyId, locked = true }).ToArray(),
                lab = new SavedLab(), playerPosition = spawn, playerYaw = spawnYaw };
            RestoreWithRollback(data, true);
        }

        private Floor01SaveData Capture()
        {
            var facts = new HashSet<string>(quests.CaptureFacts());
            // Inventory's existing observer may be one Update behind a just-completed pickup.
            foreach (string item in inventory.ItemIds) facts.Add("item:" + item);
            return new Floor01SaveData { version = 1, scene = "Floor01_Prototype", savedUtc = DateTime.UtcNow.ToString("O"),
                questIds = quests.Definitions.Select(q => q.Id).ToArray(), facts = quests.Definitions.Select(q => q.CompletionEvent).Where(facts.Contains).ToArray(),
                inventory = inventory.ItemIds.ToArray(), doors = doors.Select(d => new SavedDoor { keyId = d.RequiredKeyId, locked = d.IsLocked, open = d.IsOpen }).ToArray(),
                cabinetUnlocked = cabinet.IsUnlocked, sequenceSolved = sequence.IsSolved,
                lab = new SavedLab { power = lab.PowerOn, instructions = lab.InstructionsRead, connected = lab.ConnectedCount, data = lab.DataAccessed, complete = lab.FloorCompleted },
                playerPosition = player.transform.position, playerYaw = player.transform.eulerAngles.y };
        }

        private void Validate(Floor01SaveData data)
            => ValidateSnapshot(data, quests.Definitions, pickups.Select(p => p.ItemId), inventory.Capacity);

        /// <summary>Validates the unchanged V1 format without keeping references to an unloaded scene.</summary>
        internal static void ValidateSnapshot(Floor01SaveData data, IReadOnlyList<QuestDefinition> definitions, IEnumerable<string> itemIds, int capacity)
        {
            if (data == null || data.version != 1 || data.scene != "Floor01_Prototype" || data.questIds == null || !data.questIds.SequenceEqual(definitions.Select(q => q.Id)))
                throw new InvalidDataException("Sai phiên bản, Scene hoặc danh sách nhiệm vụ.");
            var knownFacts = new HashSet<string>(definitions.Select(q => q.CompletionEvent));
            var knownItems = new HashSet<string>(itemIds);
            if (data.facts == null || data.inventory == null || data.inventory.Length > capacity || data.lab == null || data.doors == null || data.doors.Length != knownItems.Count ||
                data.facts.Any(f => f == null || !knownFacts.Contains(f)) || data.facts.Distinct().Count() != data.facts.Length ||
                data.inventory.Any(i => i == null || !knownItems.Contains(i)) || data.inventory.Distinct().Count() != data.inventory.Length)
                throw new InvalidDataException("Dữ liệu nhiệm vụ/vật phẩm không hợp lệ.");
            var facts = new HashSet<string>(data.facts); var items = new HashSet<string>(data.inventory);
            var doorIds = new HashSet<string>();
            foreach (var door in data.doors)
                if (door == null || !knownItems.Contains(door.keyId) || !doorIds.Add(door.keyId) || (door.locked && door.open) || (!door.locked && !items.Contains(door.keyId)))
                    throw new InvalidDataException("Trạng thái cửa không nhất quán.");
            foreach (string item in knownItems) if (facts.Contains("item:" + item) != items.Contains(item)) throw new InvalidDataException("Quest và Inventory không khớp.");
            if (data.cabinetUnlocked != facts.Contains("cabinet:A101") || data.sequenceSolved != facts.Contains("sequence:A102") ||
                (items.Contains("Key_A102") && !data.cabinetUnlocked) || (items.Contains("Key_Lab") && !data.sequenceSolved))
                throw new InvalidDataException("Trạng thái phần thưởng không nhất quán.");
            foreach (var pair in new[] { new[] { "room:A101", "Key_A101" }, new[] { "room:A102", "Key_A102" }, new[] { "room:LAB", "Key_Lab" } })
                if (facts.Contains(pair[0]) && (data.doors.Single(d => d.keyId == pair[1]).locked || !items.Contains(pair[1]))) throw new InvalidDataException("Vùng nhiệm vụ chưa được mở khóa.");
            if (data.sequenceSolved && (!facts.Contains("room:A102") || !new[] { "clue:A102:A", "clue:A102:B", "clue:A102:C" }.All(facts.Contains)))
                throw new InvalidDataException("Thiếu manh mối A102.");
            var state = data.lab;
            if (state.connected < 0 || state.connected > 3 || (state.power && !facts.Contains("lab:inspect")) || (facts.Contains("lab:inspect") && !facts.Contains("room:LAB")) ||
                (state.instructions && !state.power) || (state.connected > 0 && !state.instructions) || (state.data && state.connected != 3) || (state.complete && !state.data) ||
                state.power != facts.Contains("lab:power") || (state.connected == 3) != facts.Contains("lab:connected") || state.data != facts.Contains("lab:data") || state.complete != facts.Contains("lab:complete"))
                throw new InvalidDataException("Trạng thái LAB không nhất quán.");
            Vector3 p = data.playerPosition;
            if (!Finite(p.x) || !Finite(p.y) || !Finite(p.z) || !Finite(data.playerYaw) || p.x < -7 || p.x > 23 || p.z < 0 || p.z > 73 || p.y < .5f || p.y > 4.5f)
                throw new InvalidDataException("Vị trí Player không hợp lệ.");
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        internal Floor01SaveData CaptureForTravel()
        {
            if (!Stable()) throw new InvalidOperationException(LastMessage);
            var data = Capture(); Validate(data);
            if (!SafePosition(data.playerPosition)) throw new InvalidDataException("Hãy đứng tại vị trí an toàn trước khi rời tầng 1.");
            return data;
        }

        /// <summary>Captures a background draft without changing manual-save menu feedback.</summary>
        internal Floor01SaveData CaptureForAutosave()
        {
            string message = LastMessage;
            try { return CaptureForTravel(); }
            finally { LastMessage = message; }
        }

        internal void RestoreForTravel(Floor01SaveData data) { Validate(data); RestoreWithRollback(data, false); }

        private bool SafePosition(Vector3 position)
        {
            float radius = Mathf.Max(.05f, player.radius - .03f);
            Vector3 center = position + player.center, offset = Vector3.up * Mathf.Max(0, player.height * .5f - player.radius);
            if (Physics.OverlapCapsule(center - offset, center + offset, radius, ~0, QueryTriggerInteraction.Ignore)
                .Any(c => !c.transform.IsChildOf(player.transform))) return false;
            return Physics.SphereCast(center - offset, radius, Vector3.down, out _, .3f, ~0, QueryTriggerInteraction.Ignore);
        }
        internal Floor01SaveData CaptureForRestore() => Capture();

        private void RestoreWithRollback(Floor01SaveData data, bool newGame)
        {
            Validate(data);
            if (restoring) throw new InvalidOperationException("Restore is already in progress.");
            var previous = Capture();
            var autosaveLease = GetComponent<Floor01Autosave>()?.SuspendForRestore();
            restoring = true;
            try
            {
                bool fallback = LastLoadUsedFallback;
                try { Apply(data); }
                catch (Exception restoreError)
                {
                    // Rollback restores the exact prior pose, even if that pose is now obstructed.
                    // It must not use the failed checkpoint's spawn-fallback policy.
                    try { Apply(previous, true); LastLoadUsedFallback = fallback; }
                    catch (Exception rollbackError)
                    {
                        RecoveryRequired = true;
                        throw new AggregateException("Khôi phục và rollback đều thất bại; cần tải lại khu vực.", restoreError, rollbackError);
                    }
                    throw;
                }
                RecoveryRequired = false;
                RestoreCount++;
                // Observers run after commit. Their errors cannot turn a committed restore into false.
                if (Restored != null) foreach (Action<bool> handler in Restored.GetInvocationList())
                    try { handler(newGame); }
                    catch (Exception error) { Debug.LogWarning("Save restored; observer failed: " + error.Message, this); }
            }
            finally { restoring = false; autosaveLease?.Dispose(); }
        }
        private void Apply(Floor01SaveData data, bool rollback = false)
        {
            bool controllerEnabled = player.enabled; player.enabled = false;
            try
            {
                inventory.RestoreItems(data.inventory); quests.RestoreFacts(data.facts);
                foreach (var door in doors) { var saved = data.doors.Single(d => d.keyId == door.RequiredKeyId); door.RestoreState(saved.locked, saved.open); }
                foreach (var pickup in pickups) pickup.RestoreState(inventory.HasItem(pickup.ItemId), pickup.ItemId == "Key_A101");
                cabinet.RestoreState(data.cabinetUnlocked); sequence.RestoreState(data.sequenceSolved);
                lab.RestoreState(data.lab.power, data.lab.instructions, data.lab.connected, data.lab.data, data.lab.complete);
                Physics.SyncTransforms(); LastLoadUsedFallback = !rollback && !SafePosition(data.playerPosition);
                Vector3 position = LastLoadUsedFallback ? spawn : data.playerPosition;
                if (!rollback && !SafePosition(position)) throw new InvalidDataException("Không tìm thấy vị trí đứng an toàn.");
                player.transform.SetPositionAndRotation(position, Quaternion.Euler(0, LastLoadUsedFallback ? spawnYaw : data.playerYaw, 0));
                Physics.SyncTransforms();
            }
            finally { player.enabled = controllerEnabled; }
        }
    }
}
