using HUSEscape.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HUSEscape.Quests
{
    /// <summary>Three small discovery points, sharing the existing quest facts and information modal.</summary>
    [DefaultExecutionOrder(-9100)]
    public sealed class OfficeDiscovery : MonoBehaviour
    {
        public JourneySceneEntry entry;
        public string fact, title;
        [TextArea(3,8)] public string content;
        public bool useF;
        public float radius = 1.8f;
        private bool Near => entry != null && entry.player != null && Vector3.Distance(transform.position,entry.player.transform.position) < radius;
        private bool Available => Near && entry.Session != null && !entry.Session.IsLoading && !entry.Gate.IsBlocked && !entry.player.GetComponent<InventoryUI>().IsOpen;
        private bool Selected => InteractionSelection.IsNearest(this, entry.player.transform, p => p.gameObject.scene == gameObject.scene && p.Available, false);
        private void Update()
        {
            if (!Available || !Selected || Keyboard.current == null || !(useF ? Keyboard.current.fKey.wasPressedThisFrame : Keyboard.current.eKey.wasPressedThisFrame)) return;
            if (fact == "office:computer" && (!entry.quests.HasObserved("office:notice") || !entry.quests.HasObserved("office:document")))
            { entry.ui.ShowInformation(title,"Hãy khảo sát bảng thông báo và đọc tài liệu hành chính trước khi xác nhận tuyến đường."); return; }
            entry.quests.Signal(fact); entry.ui.ShowInformation(title,content);
        }
        private void OnGUI()
        {
            if (Available && Selected) GUI.Box(new Rect(Screen.width*.25f,Screen.height*.84f,Screen.width*.5f,60),(useF ? "F — " : "E — ")+title);
        }
    }
}
