using HUSEscape.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HUSEscape.Quests
{
    /// <summary>Small scene-local exploration point using the same quest facts and input gate.</summary>
    public sealed class CampusDiscovery : MonoBehaviour
    {
        public JourneySceneEntry entry;
        public string fact;
        public string description;
        public bool automatic;
        public float radius = 2.5f;
        private bool Near => entry != null && entry.player != null && Vector3.Distance(transform.position, entry.player.transform.position) < radius;
        private void Update()
        {
            if (!Near || entry.Session == null || entry.Session.IsLoading || entry.Gate.IsBlocked || entry.player.GetComponent<InventoryUI>().IsOpen) return;
            if (automatic || (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)) entry.quests.Signal(fact);
        }
        private void OnGUI()
        {
            if (automatic || !Near || entry.Gate.IsBlocked) return;
            GUI.Box(new Rect(Screen.width * .25f, Screen.height * .84f, Screen.width * .5f, 55), "E — " + description);
        }
    }
}
