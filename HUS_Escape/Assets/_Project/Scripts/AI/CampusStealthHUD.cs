using UnityEngine;

namespace HUSEscape.AI
{
    /// <summary>Scene-local stealth feedback. Dog/guard binding is owned by the existing scene composition.</summary>
    public sealed class CampusStealthHUD : MonoBehaviour
    {
        [SerializeField] private PatrolGuard guard, dog;
        private GUIStyle style;
        private void OnGUI()
        {
            if (guard == null || dog == null) return;
            bool alert = guard.State == GuardState.Alert || dog.State == GuardState.Alert;
            bool suspicious = guard.State == GuardState.Suspicious || dog.State == GuardState.Suspicious ||
                guard.State == GuardState.Search || dog.State == GuardState.Search;
            if (style == null) style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true, normal = { textColor = Color.white } };
            style.fontSize = Mathf.Clamp(Screen.height / 42, 16, 26);
            var rect = new Rect(Screen.width * .43f, Screen.height * .035f, Screen.width * .30f, Screen.height * .085f);
            Color old = GUI.color; GUI.color = alert ? new Color(.5f,.06f,.05f,1) : suspicious ? new Color(.35f,.23f,.03f,1) : new Color(.02f,.15f,.1f,1);
            GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = old;
            GUI.Label(rect, (alert ? "ĐÃ BỊ PHÁT HIỆN" : suspicious ? "ĐANG BỊ NGHI NGỜ" : "AN TOÀN") + (dog.IsBarking ? "\nCHÓ SỦA — BÁO ĐỘNG" : ""), style);
        }
    }
}
