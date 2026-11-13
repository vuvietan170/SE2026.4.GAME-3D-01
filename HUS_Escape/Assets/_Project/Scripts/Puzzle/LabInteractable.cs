using UnityEngine;

namespace HUSEscape.Quests
{
    public enum LabInteractionKind { Computer, Breaker, DeviceA, DeviceB, DeviceC }
    /// <summary>One physical LAB target, selected by the existing nearest-target helper.</summary>
    [DisallowMultipleComponent]
    public sealed class LabInteractable : MonoBehaviour
    {
        [SerializeField] private LabRestoreSystem system;
        [SerializeField] private LabInteractionKind kind;
        [SerializeField, Min(.1f)] private float interactionDistance = 1.6f;
        public LabRestoreSystem System => system;
        public LabInteractionKind Kind => kind;
        public string DisplayName => kind == LabInteractionKind.Computer ? "Máy tính trung tâm" :
            kind == LabInteractionKind.Breaker ? "Cầu dao LAB" : "Thiết bị " + (char)('A' + (int)kind - (int)LabInteractionKind.DeviceA);
        private bool IsNear(Transform player) => isActiveAndEnabled && system != null && system.isActiveAndEnabled && player != null &&
            player.gameObject.scene == gameObject.scene && Vector3.Distance(player.position, transform.position) <= interactionDistance;
        /// <summary>Uses the shared interaction selector; only one LAB target can own the E request.</summary>
        public bool IsSelected(Transform player) => IsNear(player) && InteractionSelection.IsNearest(this, player, candidate => candidate.IsNear(player), false);
        /// <summary>Applies an accepted physical interaction, returning modal feedback.</summary>
        public string Interact() => system.Interact(kind);
    }
}
