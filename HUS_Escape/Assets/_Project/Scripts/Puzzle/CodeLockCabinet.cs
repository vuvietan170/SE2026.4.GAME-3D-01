using System.Collections;
using UnityEngine;
namespace HUSEscape.Quests
{
    /// <summary>Four-digit lock; releases the existing pickup only after its own cabinet leaf opens.</summary>
    [DefaultExecutionOrder(-11000)]
    public sealed class CodeLockCabinet : MonoBehaviour
    {
        [SerializeField] private QuestManager quests;
        [SerializeField] private string eventId = "cabinet:A101";
        [SerializeField] private string correctCode = "2018";
        [SerializeField] private Transform doorPivot;
        [SerializeField] private GameObject keyPickup;
        [SerializeField] private GameObject keyLabels;
        [SerializeField, Min(.1f)] private float interactionDistance = 1.6f;
        [SerializeField, Min(.01f)] private float openDuration = .45f;
        [SerializeField] private float openAngle = -90f;
        private Quaternion closed;
        private bool released;
        public bool IsUnlocked { get; private set; }
        public bool IsOpening { get; private set; }
        /// <summary>Restores a settled cabinet without animation, quest events or granting an item.</summary>
        public void RestoreState(bool unlocked)
        {
            StopAllCoroutines(); IsUnlocked = unlocked; IsOpening = false; released = unlocked;
            doorPivot.localRotation = closed * Quaternion.Euler(0, unlocked ? openAngle : 0, 0);
            bool visible = unlocked && !keyPickup.GetComponent<PickupItem>().IsCollected;
            keyPickup.SetActive(visible); if (keyLabels != null) keyLabels.SetActive(visible);
        }
        public bool IsNear(Transform player) => isActiveAndEnabled && !IsUnlocked && Vector3.Distance(player.position, transform.position) <= interactionDistance;
        private void Awake()
        {
            if (doorPivot == null || keyPickup == null || quests == null) { Debug.LogError("Cabinet references are incomplete.", this); enabled = false; return; }
            closed = doorPivot.localRotation; keyPickup.SetActive(false); if (keyLabels != null) keyLabels.SetActive(false);
        }
        /// <summary>Accepts exactly four ASCII digits once. Wrong codes leave the reward inaccessible.</summary>
        public bool TryUnlock(string code)
        {
            if (!isActiveAndEnabled || IsUnlocked || code == null || code.Length != 4) return false;
            foreach (char digit in code) if (digit < '0' || digit > '9') return false;
            if (code != correctCode) return false;
            IsUnlocked = true; IsOpening = true; quests.Signal(eventId); StartCoroutine(Open()); return true;
        }
        private IEnumerator Open()
        {
            float elapsed = 0;
            while (elapsed < openDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                doorPivot.localRotation = closed * Quaternion.Euler(0, openAngle * Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / openDuration)), 0);
                yield return null;
            }
            doorPivot.localRotation = closed * Quaternion.Euler(0, openAngle, 0); IsOpening = false;
            released = true; keyPickup.SetActive(true); if (keyLabels != null) keyLabels.SetActive(true);
        }
        private void LateUpdate() { if (released && keyPickup != null && !keyPickup.activeSelf && keyLabels != null) keyLabels.SetActive(false); }
    }
}
