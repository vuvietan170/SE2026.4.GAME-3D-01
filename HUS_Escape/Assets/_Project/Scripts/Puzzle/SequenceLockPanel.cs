using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HUSEscape.Quests
{
    /// <summary>A three-event chronology lock, using the existing quest facts and Inventory pickup.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(-11000)]
    public sealed class SequenceLockPanel : MonoBehaviour
    {
        [SerializeField] private QuestManager quests;
        [SerializeField] private string roomEvent = "room:A102";
        [SerializeField] private string solvedEvent = "sequence:A102";
        [SerializeField] private string[] requiredClues = { "clue:A102:A", "clue:A102:B", "clue:A102:C" };
        [SerializeField] private string[] eventNames = { "A", "B", "C" };
        [SerializeField] private int[] eventYears = { 2016, 2020, 2018 };
        [SerializeField] private Transform shutter;
        [SerializeField] private Vector3 openOffset = new Vector3(0, .95f, 0);
        [SerializeField, Min(.01f)] private float openDuration = .4f;
        [SerializeField] private GameObject keyPickup;
        [SerializeField] private GameObject keyLabels;
        [SerializeField, Min(.1f)] private float interactionDistance = 1.6f;
        private Vector3 closedPosition;
        private bool released;
        public bool IsSolved { get; private set; }
        public bool IsOpening { get; private set; }
        /// <summary>Restores a settled shutter and reward visibility without solving again.</summary>
        public void RestoreState(bool solved)
        {
            StopAllCoroutines(); IsSolved = solved; IsOpening = false; released = solved; Feedback = "";
            shutter.localPosition = closedPosition + (solved ? openOffset : Vector3.zero);
            bool visible = solved && !keyPickup.GetComponent<PickupItem>().IsCollected;
            keyPickup.SetActive(visible); if (keyLabels != null) keyLabels.SetActive(visible);
        }
        public string Feedback { get; private set; } = "";
        public IReadOnlyList<string> EventNames => eventNames;
        public int ReadClueCount
        {
            get
            {
                int count = 0;
                if (quests != null) foreach (string fact in requiredClues) if (quests.HasObserved(fact)) count++;
                return count;
            }
        }
        public bool IsNear(Transform player) => isActiveAndEnabled && !IsSolved && player != null &&
            Vector3.Distance(player.position, transform.position) <= interactionDistance;
        private void Awake()
        {
            // Fail closed: an incomplete configuration must not expose the reward.
            if (keyPickup != null) keyPickup.SetActive(false);
            if (keyLabels != null) keyLabels.SetActive(false);
            if (quests == null || shutter == null || keyPickup == null || requiredClues == null || requiredClues.Length != 3 ||
                eventNames == null || eventNames.Length != 3 || eventYears == null || eventYears.Length != 3 ||
                new HashSet<string>(requiredClues).Count != 3 || new HashSet<string>(eventNames).Count != 3 ||
                new HashSet<int>(eventYears).Count != 3)
            {
                Debug.LogError("A102 sequence lock requires three distinct clues/events/years and explicit scene references.", this);
                enabled = false; return;
            }
            closedPosition = shutter.localPosition;
        }
        /// <summary>Accepts a unique permutation in chronological order after room entry and all three clues.</summary>
        public bool TrySolve(IReadOnlyList<int> selection)
        {
            if (!isActiveAndEnabled || IsSolved) return false;
            if (!quests.HasObserved(roomEvent) || ReadClueCount != requiredClues.Length)
            { Feedback = "Hãy vào A102 và đọc đủ ba manh mối trước khi xác nhận."; return false; }
            if (selection == null || selection.Count != 3)
            { Feedback = "Chọn đủ ba sự kiện, mỗi sự kiện một lần."; return false; }
            var seen = new HashSet<int>();
            for (int i = 0; i < selection.Count; i++)
                if (selection[i] < 0 || selection[i] >= eventYears.Length || !seen.Add(selection[i]))
                { Feedback = "Mỗi sự kiện chỉ được chọn một lần."; return false; }
            for (int i = 1; i < selection.Count; i++)
                if (eventYears[selection[i - 1]] >= eventYears[selection[i]])
                { Feedback = "Thứ tự chưa đúng. Hãy sắp từ năm sớm nhất đến năm muộn nhất."; return false; }
            IsSolved = true; IsOpening = true; Feedback = "Đúng thứ tự! Đang mở ngăn chứa Key_Lab.";
            quests.Signal(solvedEvent); StartCoroutine(Open()); return true;
        }
        private IEnumerator Open()
        {
            float elapsed = 0;
            while (elapsed < openDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                shutter.localPosition = closedPosition + openOffset * Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / openDuration));
                yield return null;
            }
            shutter.localPosition = closedPosition + openOffset;
            IsOpening = false; released = true; keyPickup.SetActive(true);
            if (keyLabels != null) keyLabels.SetActive(true);
        }
        private void LateUpdate()
        {
            if (released && keyPickup != null && !keyPickup.activeSelf && keyLabels != null) keyLabels.SetActive(false);
        }
    }
}
