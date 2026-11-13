using UnityEngine;
namespace HUSEscape.Quests
{
    /// <summary>Readable scene clue; emits a fact without becoming an inventory item.</summary>
    public sealed class ClueNote : MonoBehaviour
    {
        [SerializeField, TextArea(3, 8)] private string content = "Mã tủ là năm thành lập của một câu lạc bộ giả định: 2018.";
        [SerializeField] private string eventId = "clue:A101";
        [SerializeField] private string title = "MANH MỐI A101";
        [SerializeField, Min(.1f)] private float interactionDistance = 1.6f;
        public string Content => content;
        public string Title => title;
        public bool IsNear(Transform player) => isActiveAndEnabled && Vector3.Distance(player.position, transform.position) <= interactionDistance;
        /// <summary>Marks this clue read; repeated reads do not repeat quest completion.</summary>
        public void Read(QuestManager quests) => quests.Signal(eventId);
    }
}
