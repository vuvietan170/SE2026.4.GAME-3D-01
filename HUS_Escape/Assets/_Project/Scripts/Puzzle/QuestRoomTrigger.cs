using HUSEscape.Inventory;
using UnityEngine;
namespace HUSEscape.Quests
{
    /// <summary>Emits a room-entry fact for the explicitly bound scene Player.</summary>
    public sealed class QuestRoomTrigger : MonoBehaviour
    {
        [SerializeField] private QuestManager quests;
        [SerializeField] private string eventId;
        private void OnTriggerEnter(Collider other) => Observe(other);
        private void OnTriggerStay(Collider other) => Observe(other);
        private void Observe(Collider other)
        {
            if (quests != null && other.GetComponentInParent<PlayerInventory>() == quests.Inventory)
                quests.Signal(eventId);
        }
    }
}
