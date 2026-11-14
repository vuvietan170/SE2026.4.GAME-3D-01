using HUSEscape.Player;
using HUSEscape.Persistence;
using HUSEscape.World;
using System.Collections.Generic;
using UnityEngine;
namespace HUSEscape.Quests
{
    public enum QuestModal { None, Journal, Clue, Code, Sequence, Lab, Progress, Information }
    /// <summary>Scene-local HUD, journal and A101 dialogs using the project's existing IMGUI approach.</summary>
    [DefaultExecutionOrder(-9000)]
    public sealed class QuestPuzzleUI : MonoBehaviour, IGameplayPauseOwner
    {
        [SerializeField] private QuestManager quests;
        [SerializeField] private GameplayInputGate inputGate;
        [SerializeField] private InventoryUI inventoryUI;
        [SerializeField] private Floor01SaveSystem saveSystem;
        private JourneySession journey;
        private string informationTitle, informationContent;
        /// <summary>Displays a scene interaction using the existing modal, scroll view and input gate.</summary>
        public void ShowInformation(string title, string content)
        {
            if (Mode != QuestModal.None || inputGate == null || inputGate.IsBlocked || (inventoryUI != null && inventoryUI.IsOpen)) return;
            informationTitle = title; informationContent = content; Open(QuestModal.Information);
        }
        private bool presentationRestored;
        internal bool CanBindJourney(QuestManager expectedQuests, GameplayInputGate expectedGate) => quests == expectedQuests && inputGate == expectedGate;
        /// <summary>Binds only the active scene to its instance-owned travel coordinator.</summary>
        public void BindJourney(JourneySession session) { journey = session; }
        private readonly Queue<string> notices = new Queue<string>();
        private string notice = "";
        private float noticeRemaining;
        private int confirmation;
        public int NotificationCount { get; private set; }
        public string VisibleNotification => notice;
        private GUIStyle bodyStyle, titleStyle, buttonStyle, smallStyle;
        private void OnEnable()
        {
            if (inputGate != null) inputGate.Deactivated += CloseModal;
            if (quests != null) quests.ProgressRecorded += OnProgress;
            if (saveSystem != null) saveSystem.Restored += ProgressRestored;
        }
        private void Start() { if (quests != null && !presentationRestored) EnqueueNotice("Mục tiêu mới\n" + quests.CurrentObjective); }
        private void EnqueueNotice(string value)
        {
            if (notices.Count >= 4) notices.Dequeue();
            notices.Enqueue(value); NotificationCount++;
        }
        private void OnProgress(string fact, int previous, int current)
        {
            if (fact == "lab:complete") EnqueueNotice("HOÀN THÀNH TẦNG 1\nToàn bộ 17 mục tiêu đã hoàn thành. Tìm lối ra cuối hành lang.");
            else if (current != previous)
            {
                string heading = fact == "cabinet:A101" || fact == "sequence:A102" || fact == "lab:connected" ? "Giải đúng câu đố" : "Hoàn thành mục tiêu";
                EnqueueNotice(heading + "\nMục tiêu mới: " + quests.CurrentObjective);
            }
            else if (fact.StartsWith("clue:")) EnqueueNotice("Đã ghi nhận manh mối\n" + quests.CurrentObjective);
            else if (fact == "cabinet:A101" || fact == "sequence:A102" || fact == "lab:connected") EnqueueNotice("Giải đúng câu đố\nTiến trình đã được ghi nhận.");
        }
        /// <summary>Clears stale modal/notification state after a silent restore.</summary>
        public void ProgressRestored(bool newGame)
        {
            presentationRestored = true;
            CloseModal(); confirmation = 0; notices.Clear(); notice = ""; noticeRemaining = 0;
            if (newGame) EnqueueNotice("Mục tiêu mới\n" + quests.CurrentObjective);
        }
        private ClueNote currentNote;
        private CodeLockCabinet currentCabinet;
        private SequenceLockPanel currentSequence;
        private LabInteractable currentLab;
        private readonly System.Collections.Generic.List<int> selectedEvents = new System.Collections.Generic.List<int>(3);
        private string enteredCode = "";
        private string message = "";
        private Vector2 scroll;
        public QuestModal Mode { get; private set; }
        public bool IsPauseOpen => Mode != QuestModal.None;
        public string EnteredCode => enteredCode;
        public string Message => message;
        public System.Collections.Generic.IReadOnlyList<int> SelectedEvents => selectedEvents;
        private void Update()
        {
            if (quests == null || quests.Inventory == null || inputGate == null) return;
            if (Mode == QuestModal.None)
            {
                if (noticeRemaining <= 0 && notices.Count > 0) { notice = notices.Dequeue(); noticeRemaining = 4f; }
                noticeRemaining -= Time.unscaledDeltaTime;
                if (noticeRemaining <= 0) notice = "";
            }
            if (Mode != QuestModal.None)
            {
                if (!inputGate.IsTopModal(this)) return;
                if (inputGate.ConsumeUi("Cancel", this) || (Mode == QuestModal.Journal && inputGate.ConsumeUi("Journal", this))) { CloseModal(); return; }
                if (Mode == QuestModal.Code)
                {
                    for (int i = 0; i < 10; i++) if (inputGate.UiPressed("Digit" + i)) AppendDigit(i);
                    if (inputGate.UiPressed("Backspace") && enteredCode.Length > 0) enteredCode = enteredCode.Substring(0, enteredCode.Length - 1);
                    if (inputGate.UiPressed("Clear")) enteredCode = "";
                    if (inputGate.UiPressed("Submit")) SubmitCode();
                }
                else if (Mode == QuestModal.Sequence)
                {
                    if (inputGate.UiPressed("SequenceA") || inputGate.UiPressed("Digit1")) SelectEvent(0);
                    if (inputGate.UiPressed("SequenceB") || inputGate.UiPressed("Digit2")) SelectEvent(1);
                    if (inputGate.UiPressed("SequenceC") || inputGate.UiPressed("Digit3")) SelectEvent(2);
                    if (inputGate.UiPressed("Backspace") && selectedEvents.Count > 0) selectedEvents.RemoveAt(selectedEvents.Count - 1);
                    if (inputGate.UiPressed("Clear")) selectedEvents.Clear();
                    if (inputGate.UiPressed("Submit")) SubmitSequence();
                }
                else if (Mode == QuestModal.Lab && (inputGate.UiPressed("Submit")))
                {
                    if (CanConfirmLab()) ConfirmLabCompletion(); else CloseModal();
                }
                return;
            }
            if (inputGate.IsBlocked || (inventoryUI != null && inventoryUI.IsOpen)) return;
            if (inputGate.UiPressed("Save") && (saveSystem != null || journey != null)) { confirmation = 0; Open(QuestModal.Progress); return; }
            if (inputGate.UiPressed("Journal")) { Open(QuestModal.Journal); return; }
            var note = NearestNote(); var cabinet = NearestCabinet();
            // A modal target takes this frame before legacy E/F updates; the input gate prevents a double interaction.
            if (inputGate.PlayerPressed("Pickup") && note != null) { currentNote = note; Open(QuestModal.Clue); note.Read(quests); return; }
            if (inputGate.PlayerPressed("Interact") && cabinet != null) { currentCabinet = cabinet; enteredCode = ""; Open(QuestModal.Code); }
            else if (inputGate.PlayerPressed("Interact"))
            {
                var panel = NearestSequence();
                if (panel != null) { currentSequence = panel; selectedEvents.Clear(); Open(QuestModal.Sequence); }
                else
                {
                    var lab = NearestLab();
                    if (lab != null) { currentLab = lab; Open(QuestModal.Lab); message = lab.Interact(); }
                }
            }
        }
        private ClueNote NearestNote()
        {
            ClueNote best = null; float distance = float.PositiveInfinity;
            foreach (var note in FindObjectsByType<ClueNote>())
            {
                if (note.gameObject.scene != gameObject.scene || !note.IsNear(quests.Inventory.transform)) continue;
                float d = (note.transform.position - quests.Inventory.transform.position).sqrMagnitude;
                if (d < distance) { best = note; distance = d; }
            }
            return best;
        }
        private CodeLockCabinet NearestCabinet()
        {
            CodeLockCabinet best = null; float distance = float.PositiveInfinity;
            foreach (var cabinet in FindObjectsByType<CodeLockCabinet>())
            {
                if (cabinet.gameObject.scene != gameObject.scene || !cabinet.IsNear(quests.Inventory.transform)) continue;
                float d = (cabinet.transform.position - quests.Inventory.transform.position).sqrMagnitude;
                if (d < distance) { best = cabinet; distance = d; }
            }
            return best;
        }
        private void Open(QuestModal mode) { if (inputGate != null && inputGate.Acquire(this)) { message = ""; Mode = mode; } }
        private LabInteractable NearestLab()
        {
            foreach (var target in FindObjectsByType<LabInteractable>())
                if (target.gameObject.scene == gameObject.scene && target.IsSelected(quests.Inventory.transform)) return target;
            return null;
        }
        private bool CanConfirmLab() => currentLab != null && currentLab.Kind == LabInteractionKind.Computer &&
            currentLab.System.DataAccessed && !currentLab.System.FloorCompleted;
        /// <summary>Confirms the displayed exit guidance using the same path for Enter and mouse.</summary>
        public bool ConfirmLabCompletion()
        {
            if (Mode != QuestModal.Lab || !CanConfirmLab() || !currentLab.System.ConfirmCompletion()) return false;
            CloseModal(); return true;
        }
        private SequenceLockPanel NearestSequence()
        {
            SequenceLockPanel best = null; float distance = float.PositiveInfinity;
            foreach (var panel in FindObjectsByType<SequenceLockPanel>())
            {
                if (panel.gameObject.scene != gameObject.scene || !panel.IsNear(quests.Inventory.transform)) continue;
                float d = (panel.transform.position - quests.Inventory.transform.position).sqrMagnitude;
                if (d < distance) { best = panel; distance = d; }
            }
            return best;
        }
        /// <summary>Closes any quest dialog, retaining a one-frame input release guard.</summary>
        public void CloseModal() { Mode = QuestModal.None; currentNote = null; currentCabinet = null; currentSequence = null; currentLab = null; selectedEvents.Clear(); enteredCode = ""; if (inputGate != null) inputGate.Release(this); }
        /// <summary>Selects one event once; both keyboard and mouse use this path.</summary>
        public void SelectEvent(int index)
        {
            if (Mode != QuestModal.Sequence || currentSequence == null || index < 0 || index >= currentSequence.EventNames.Count) return;
            if (selectedEvents.Contains(index)) { message = "Sự kiện này đã được chọn. Dùng Backspace hoặc Xóa để sửa."; return; }
            selectedEvents.Add(index); message = "";
        }
        /// <summary>Submits chronology to the A102 lock; never grants inventory items directly.</summary>
        public bool SubmitSequence()
        {
            if (Mode != QuestModal.Sequence || currentSequence == null) return false;
            if (!currentSequence.TrySolve(selectedEvents)) { message = currentSequence.Feedback; return false; }
            CloseModal(); return true;
        }
        /// <summary>Appends a keypad digit; the same API serves mouse and keyboard input.</summary>
        public void AppendDigit(int digit) { if (Mode == QuestModal.Code && enteredCode.Length < 4 && digit >= 0 && digit <= 9) enteredCode += digit.ToString(); }
        /// <summary>Submits the entered PIN to the real cabinet, without granting an inventory item directly.</summary>
        public bool SubmitCode()
        {
            if (Mode != QuestModal.Code || currentCabinet == null) return false;
            if (!currentCabinet.TryUnlock(enteredCode)) { message = "Mã chưa đúng. Nhập đủ 4 số rồi thử lại."; return false; }
            CloseModal(); return true;
        }
        private void OnDisable()
        {
            if (inputGate != null) inputGate.Deactivated -= CloseModal;
            if (quests != null) quests.ProgressRecorded -= OnProgress;
            if (saveSystem != null) saveSystem.Restored -= ProgressRestored;
            CloseModal();
        }
        private void EnsureStyles()
        {
            if (bodyStyle != null) return;
            bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, wordWrap = true, richText = false };
            bodyStyle.normal.textColor = Color.white;
            titleStyle = new GUIStyle(bodyStyle) { fontSize = 26, fontStyle = FontStyle.Bold };
            smallStyle = new GUIStyle(bodyStyle) { fontSize = 19 };
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 21, wordWrap = true, padding = new RectOffset(12, 12, 8, 8) };
        }
        private static void OpaquePanel(Rect rect)
        {
            Color old = GUI.color; GUI.color = new Color(.035f, .045f, .065f, 1);
            GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = new Color(.18f, .65f, .82f, 1);
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 3), Texture2D.whiteTexture); GUI.color = old;
        }
        private bool Button(string text) => GUILayout.Button(text, buttonStyle, GUILayout.MinHeight(46));
        private void OnGUI()
        {
            if (quests == null || quests.Inventory == null) return;
            EnsureStyles(); Matrix4x4 previousMatrix = GUI.matrix; int previousDepth = GUI.depth;
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1280 * scale) / 2, (Screen.height - 720 * scale) / 2), Quaternion.identity, Vector3.one * scale);
            GUI.depth = -100;
            if (Mode == QuestModal.None)
            {
                OpaquePanel(new Rect(16, 16, 500, 148));
                GUI.Label(new Rect(32, 27, 468, 30), "NHIỆM VỤ — J: nhật ký", titleStyle);
                GUI.Label(new Rect(32, 65, 468, 92), quests.CurrentObjective, smallStyle);
                bool canOpen = inputGate != null && !inputGate.IsBlocked && (inventoryUI == null || !inventoryUI.IsOpen);
                if (canOpen && (saveSystem != null || journey != null) && GUI.Button(new Rect(986, 18, 278, 48), "Tiến trình / F5", buttonStyle)) { confirmation = 0; Open(QuestModal.Progress); }
                if (canOpen)
                {
                    string prompt = NearestNote() != null ? "F — Đọc manh mối" : NearestCabinet() != null ? "E — Nhập mã tủ A101" : NearestSequence() != null ? "E — Sắp xếp sự kiện A102" : "";
                    if (prompt.Length == 0) { var target = NearestLab(); if (target != null) prompt = "E — " + target.DisplayName; }
                    if (prompt.Length > 0) { OpaquePanel(new Rect(390, 620, 500, 62)); GUI.Label(new Rect(408, 637, 464, 35), prompt, bodyStyle); }
                }
                if (notice.Length > 0 && noticeRemaining > 0)
                {
                    OpaquePanel(new Rect(770, 85, 494, 150)); GUI.Label(new Rect(788, 102, 458, 120), notice, smallStyle);
                }
            }
            else if (inputGate != null && inputGate.IsTopModal(this)) DrawModal();
            GUI.depth = previousDepth; GUI.matrix = previousMatrix;
        }
        private void DrawModal()
        {
            Rect panel = new Rect(260, 50, 760, 620); OpaquePanel(panel);
            string title = Mode == QuestModal.Information ? informationTitle : Mode == QuestModal.Journal ? "NHẬT KÝ NHIỆM VỤ" : Mode == QuestModal.Clue ? (currentNote != null ? currentNote.Title : "MANH MỐI") :
                Mode == QuestModal.Sequence ? "A102 — THỨ TỰ SỰ KIỆN" : Mode == QuestModal.Lab ? "LAB — " + (currentLab != null ? currentLab.DisplayName : "TRẠNG THÁI") :
                Mode == QuestModal.Progress ? "TIẾN TRÌNH — " + (journey != null && journey.IsTrainingOffice ? "PHÒNG ĐÀO TẠO" : journey != null && journey.IsCampus ? "CAMPUS" : "FLOOR 01") : "TỦ KHÓA A101 — 4 CHỮ SỐ";
            GUI.Label(new Rect(284, 69, 712, 42), title, titleStyle);
            GUILayout.BeginArea(new Rect(284, 123, 712, 462));
            scroll = GUILayout.BeginScrollView(scroll);
            if (Mode == QuestModal.Information) GUILayout.Label(informationContent, bodyStyle);
            else if (Mode == QuestModal.Journal)
            {
                for (int i = 0; i < quests.JournalDefinitions.Count; i++)
                {
                    string state = quests.GetJournalState(i) == QuestState.Completed ? "HOÀN THÀNH" : quests.GetJournalState(i) == QuestState.InProgress ? "ĐANG THỰC HIỆN" : "CHƯA BẮT ĐẦU";
                    GUILayout.Label(state + "\n" + quests.JournalDefinitions[i].Title, bodyStyle); GUILayout.Space(18);
                }
            }
            else if (Mode == QuestModal.Clue) GUILayout.Label(currentNote != null ? currentNote.Content : "", bodyStyle);
            else if (Mode == QuestModal.Lab) GUILayout.Label(message, bodyStyle);
            else if (Mode == QuestModal.Sequence)
            {
                GUILayout.Label("Sắp từ năm sớm nhất đến năm muộn nhất. Đã đọc " + currentSequence.ReadClueCount + "/3 manh mối.", bodyStyle); GUILayout.Space(20);
                string order = ""; foreach (int index in selectedEvents) order += (order.Length > 0 ? " → " : "") + currentSequence.EventNames[index];
                GUILayout.Label(order.Length > 0 ? order : "_ → _ → _", titleStyle); GUILayout.Space(20); GUILayout.BeginHorizontal();
                for (int i = 0; i < currentSequence.EventNames.Count; i++) if (Button(currentSequence.EventNames[i] + " [" + (i + 1) + "]")) SelectEvent(i);
                GUILayout.EndHorizontal(); GUILayout.Space(12); if (Button("Xóa thứ tự / Delete")) selectedEvents.Clear();
                GUILayout.Space(20); GUILayout.Label(message, bodyStyle); GUILayout.Space(18); GUILayout.Label("A/B/C hoặc 1/2/3; Backspace để sửa. Esc để đóng.", smallStyle);
            }
            else if (Mode == QuestModal.Code)
            {
                GUILayout.Label(enteredCode.PadRight(4, '_'), titleStyle); GUILayout.Space(14);
                for (int row = 0; row < 3; row++)
                {
                    GUILayout.BeginHorizontal(); for (int column = 0; column < 3; column++) { int digit = row * 3 + column + 1; if (Button(digit.ToString())) AppendDigit(digit); } GUILayout.EndHorizontal(); GUILayout.Space(8);
                }
                GUILayout.BeginHorizontal(); if (Button("Xóa")) enteredCode = ""; if (Button("0")) AppendDigit(0);
                if (Button("←") && enteredCode.Length > 0) enteredCode = enteredCode.Substring(0, enteredCode.Length - 1); GUILayout.EndHorizontal();
                GUILayout.Space(18); GUILayout.Label(message, bodyStyle); GUILayout.Space(12); GUILayout.Label("Phím số / numpad; Backspace để sửa. Enter xác nhận, Esc đóng.", smallStyle);
            }
            else if (Mode == QuestModal.Progress) DrawProgress();
            GUILayout.EndScrollView(); GUILayout.EndArea();
            // Footer is outside the scroll viewport: long text never overlaps these controls.
            bool action = Mode == QuestModal.Code || Mode == QuestModal.Sequence || CanConfirmLab();
            if (action)
            {
                if (GUI.Button(new Rect(284, 605, 430, 44), CanConfirmLab() ? "Hoàn thành tầng 1 / Enter" : "Xác nhận / Enter", buttonStyle))
                { if (Mode == QuestModal.Code) SubmitCode(); else if (Mode == QuestModal.Sequence) SubmitSequence(); else ConfirmLabCompletion(); }
            }
            if (GUI.Button(new Rect(action ? 732 : 756, 605, action ? 264 : 240, 44), "Đóng / Esc", buttonStyle)) CloseModal();
        }
        private void DrawProgress()
        {
            if (journey != null) { DrawJourneyProgress(); return; }
            if (saveSystem == null) { GUILayout.Label("Chưa cấu hình Save/Load cho Scene này.", bodyStyle); return; }
            GUILayout.Label(saveSystem.HasSaveFile ? "Có bản lưu trên thiết bị." : "Chưa có bản lưu trên thiết bị.", titleStyle);
            GUILayout.Space(12); GUILayout.Label("New Game chỉ đặt lại phiên chơi, không xóa bản lưu. Continue không ghi file. Lưu đè cần xác nhận riêng.", bodyStyle); GUILayout.Space(18);
            if (confirmation == 0)
            {
                if (Button("Lưu tiến trình hiện tại")) { if (saveSystem.HasSaveFile) confirmation = 1; else saveSystem.TrySave(); }
                GUILayout.Space(10); if (Button("Continue — tải bản lưu")) saveSystem.TryContinue();
                GUILayout.Space(10); if (Button("New Game — bắt đầu phiên mới")) confirmation = 2;
            }
            else
            {
                GUILayout.Label(confirmation == 1 ? "Ghi đè bản lưu bằng tiến trình hiện tại?" : "Đặt lại phiên chơi hiện tại? Bản lưu trên đĩa vẫn được giữ.", bodyStyle);
                GUILayout.Space(12);
                if (Button(confirmation == 1 ? "Xác nhận ghi đè" : "Xác nhận New Game"))
                { if (confirmation == 1) { saveSystem.TrySave(true); confirmation = 0; } else saveSystem.TryNewGame(true); }
                if (Button("Hủy")) confirmation = 0;
            }
            GUILayout.Space(18); GUILayout.Label(saveSystem.LastMessage, smallStyle);
        }
        private void DrawJourneyProgress()
        {
            GUILayout.Label("Slot — " + journey.CurrentSaveFile, titleStyle);
            GUILayout.Label("Chuyển khu vực không tự lưu. New Game giữ các file cũ. Ghi đè cần xác nhận.", bodyStyle); GUILayout.Space(12);
            if (confirmation == 0)
            {
                if (Button("Lưu khu vực hiện tại")) { if (journey.HasCurrentSave) confirmation = 1; else journey.TrySave(); }
                if (Button("Continue khu vực hiện tại")) journey.TryContinue(journey.Area);
                if (!journey.IsCampus && journey.HasCampusSave && Button("Continue Campus — slot hành trình")) journey.TryContinue(true);
                if (journey.Area != JourneyArea.Floor01 && journey.HasFloorSave && Button("Continue Floor 01 — Save V1")) journey.TryContinue(false);
                if (!journey.IsTrainingOffice && journey.HasOfficeSave && Button("Continue Phòng Đào tạo")) journey.TryContinue(JourneyArea.TrainingOffice);
                if (Button("New Game — bắt đầu lại Floor 01")) confirmation = 2;
                if (Button("Trở về menu — kết thúc phiên, giữ file lưu")) journey.TryReturnToMenu();
            }
            else
            {
                GUILayout.Label(confirmation == 1 ? "Xác nhận ghi đè slot hiện tại? Slot của khu vực kia được giữ." : "Xác nhận bắt đầu lại Floor 01? Các file lưu được giữ nguyên.", bodyStyle);
                if (Button("Xác nhận")) { if (confirmation == 1) { journey.TrySave(true); confirmation = 0; } else journey.TryNewGame(true); }
                if (Button("Hủy")) confirmation = 0;
            }
            GUILayout.Space(12); GUILayout.Label(journey.LastMessage, smallStyle);
        }
    }
}
