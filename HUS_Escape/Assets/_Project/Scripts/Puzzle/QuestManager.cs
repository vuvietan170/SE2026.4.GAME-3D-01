using System;
using System.Collections.Generic;
using HUSEscape.Inventory;
using UnityEngine;

namespace HUSEscape.Quests
{
    public enum QuestState { NotStarted, InProgress, Completed }
    [Serializable]
    public sealed class QuestDefinition
    {
        [SerializeField] private string id;
        [SerializeField] private string title;
        [SerializeField] private string completionEvent;
        public string Id => id;
        public string Title => title;
        public string CompletionEvent => completionEvent;
        public QuestDefinition() { }
        internal QuestDefinition(string key, string text, string fact) { id = key; title = text; completionEvent = fact; }
    }

    /// <summary>Small ordered quest log driven by scene-local gameplay facts, including out-of-order facts.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(-9500)]
    public sealed class QuestManager : MonoBehaviour
    {
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private QuestDefinition[] definitions = Array.Empty<QuestDefinition>();
        private readonly HashSet<string> observed = new HashSet<string>();
        private QuestState[] states;
        private readonly QuestDefinition[] campus = {
            new QuestDefinition("campus_courtyard", "Khảo sát sân trường phía trước T3.", "campus:courtyard"),
            new QuestDefinition("campus_map", "Đọc biển sơ đồ khuôn viên (E).", "campus:map"),
            new QuestDefinition("campus_direction", "Tìm biển chỉ hướng Training Office (E).", "campus:direction") };
        private readonly QuestDefinition[] office = {
            new QuestDefinition("office_notice", "Khảo sát bảng thông báo Phòng Đào tạo (E).", "office:notice"),
            new QuestDefinition("office_document", "Đọc tài liệu hành chính ở quầy tiếp nhận (F).", "office:document"),
            new QuestDefinition("office_computer", "Truy cập máy tính xác nhận tuyến ra cổng chính (E).", "office:computer") };
        private QuestDefinition[] journal, officeJournal;
        private const string officeCompleteObjective = "Đến cổng chính phía Bắc Campus và nhấn E để thoát.";
        public bool OfficeActive { get; private set; }
        public int OfficeIndex { get { int i = 0; while (i < office.Length && observed.Contains(office[i].CompletionEvent)) i++; return i; } }
        public string[] CaptureOfficeFacts() { var facts = new List<string>(); foreach (var q in office) if (observed.Contains(q.CompletionEvent)) facts.Add(q.CompletionEvent); return facts.ToArray(); }
        internal void RestoreOffice(string[] facts, bool active)
        {
            foreach (var q in office) observed.Remove(q.CompletionEvent);
            foreach (string fact in facts) observed.Add(fact);
            OfficeActive = active;
        }
        public bool CampusActive { get; private set; }
        public int CampusIndex { get { int i = 0; while (i < campus.Length && observed.Contains(campus[i].CompletionEvent)) i++; return i; } }
        public IReadOnlyList<QuestDefinition> JournalDefinitions => OfficeActive || observed.Contains("office:notice") || observed.Contains("office:document") || observed.Contains("office:computer") ? officeJournal : CampusActive ? journal : definitions;
        public QuestState GetJournalState(int i) => i >= definitions.Length + campus.Length ? (i - definitions.Length - campus.Length < OfficeIndex ? QuestState.Completed : i - definitions.Length - campus.Length == OfficeIndex ? QuestState.InProgress : QuestState.NotStarted) : i < definitions.Length ? GetState(i) : i - definitions.Length < CampusIndex ? QuestState.Completed : i - definitions.Length == CampusIndex ? QuestState.InProgress : QuestState.NotStarted;
        public string[] CaptureCampusFacts() { var facts = new List<string>(); foreach (var q in campus) if (observed.Contains(q.CompletionEvent)) facts.Add(q.CompletionEvent); return facts.ToArray(); }
        internal void RestoreCampus(string[] facts)
        {
            foreach (var q in campus) observed.Remove(q.CompletionEvent);
            foreach (string fact in facts) observed.Add(fact);
            CampusActive = true;
        }
        internal void RestoreAreas(string[] campusFacts, bool campusActive, string[] officeFacts, bool officeActive)
        {
            RestoreCampus(campusFacts); CampusActive = campusActive;
            RestoreOffice(officeFacts, officeActive);
        }
        /// <summary>One notification per newly recorded fact; silent restore never invokes this event.</summary>
        public event Action<string, int, int> ProgressRecorded;

        public string[] CaptureFacts()
        {
            var result = new List<string>();
            foreach (var definition in definitions) if (observed.Contains(definition.CompletionEvent)) result.Add(definition.CompletionEvent);
            return result.ToArray();
        }
        /// <summary>Restores validated facts without replaying gameplay or completion notifications.</summary>
        public void RestoreFacts(IReadOnlyList<string> restored)
        {
            observed.Clear(); foreach (string fact in restored) observed.Add(fact);
            Array.Clear(states, 0, states.Length); CurrentIndex = 0; Advance();
        }
        /// <summary>The existing Player inventory; no second item store is created.</summary>
        public PlayerInventory Inventory => inventory;
        public IReadOnlyList<QuestDefinition> Definitions => definitions;
        public int CurrentIndex { get; private set; }
        public bool IsComplete => CurrentIndex >= definitions.Length;
        public string CurrentObjective => OfficeActive ? (OfficeIndex < office.Length ? office[OfficeIndex].Title : "Trở lại Campus qua cửa sảnh (E), rồi đến cổng chính phía Bắc.") : CampusActive && OfficeIndex == office.Length ? officeCompleteObjective : CampusActive ? (CampusIndex < campus.Length ? campus[CampusIndex].Title : "Đến cửa Phòng Đào tạo phía Đông Bắc và nhấn E.") : IsComplete ? "Đã hoàn thành tuyến nhiệm vụ tầng 1. Quay lại cuối hành lang để ra Campus (E)." : definitions[CurrentIndex].Title;
        private void Awake()
        {
            states = new QuestState[definitions.Length];
            journal = new QuestDefinition[definitions.Length + campus.Length]; definitions.CopyTo(journal, 0); campus.CopyTo(journal, definitions.Length);
            officeJournal = new QuestDefinition[journal.Length + office.Length]; journal.CopyTo(officeJournal, 0); office.CopyTo(officeJournal, journal.Length);
            var ids = new HashSet<string>();
            foreach (var definition in definitions)
                if (definition == null || string.IsNullOrWhiteSpace(definition.Id) || string.IsNullOrWhiteSpace(definition.CompletionEvent) || !ids.Add(definition.Id))
                { Debug.LogError("Quest definitions require unique IDs and completion events.", this); enabled = false; return; }
            Advance();
        }
        private void Update()
        {
            // Adapter for the existing Inventory, which has no events. Observe facts, never copy item storage.
            if (inventory == null) return;
            foreach (var definition in definitions)
                if (definition.CompletionEvent.StartsWith("item:", StringComparison.Ordinal) && inventory.HasItem(definition.CompletionEvent.Substring(5)))
                    Signal(definition.CompletionEvent);
        }
        /// <summary>Records a known gameplay fact once; future steps can consume facts observed early.</summary>
        public bool Signal(string eventId)
        {
            if (!enabled || states == null || string.IsNullOrWhiteSpace(eventId)) return false;
            bool known = false;
            foreach (var definition in definitions) if (definition.CompletionEvent == eventId) { known = true; break; }
            if (CampusActive) foreach (var definition in campus) if (definition.CompletionEvent == eventId) known = true;
            if (OfficeActive) foreach (var definition in office) if (definition.CompletionEvent == eventId) known = true;
            int previous = CurrentIndex + (CampusActive ? CampusIndex : 0) + (OfficeActive ? OfficeIndex : 0);
            if (!known || !observed.Add(eventId)) return false;
            Advance(); ProgressRecorded?.Invoke(eventId, previous, CurrentIndex + (CampusActive ? CampusIndex : 0) + (OfficeActive ? OfficeIndex : 0)); return true;
        }
        /// <summary>Returns one entry's state for the HUD/journal.</summary>
        public QuestState GetState(int index) => states[index];
        /// <summary>Tests a recorded gameplay fact without changing quest progression.</summary>
        public bool HasObserved(string eventId) => eventId != null && observed.Contains(eventId);
        private void Advance()
        {
            while (CurrentIndex < definitions.Length)
            {
                states[CurrentIndex] = QuestState.InProgress;
                if (!observed.Contains(definitions[CurrentIndex].CompletionEvent)) break;
                states[CurrentIndex] = QuestState.Completed; CurrentIndex++;
            }
        }
    }
}
