using HUSEscape.World;
using UnityEngine;

/// <summary>Prototype call panel only. Does not move actors, change quests, save data or load scenes.</summary>
public sealed class ElevatorUnavailable : MonoBehaviour
{
    [SerializeField] private JourneySceneEntry entry;
    [SerializeField] private BuildingExit exit;
    [SerializeField] private BoxCollider interactionZone;
    private float messageUntil;
    private GUIStyle style;
    public bool CanInteract => entry != null && entry.player != null && entry.Session != null && !entry.Session.IsLoading &&
        !entry.Gate.IsBlocked && !entry.player.GetComponent<InventoryUI>().IsOpen &&
        interactionZone != null && interactionZone.enabled && interactionZone.gameObject.activeInHierarchy &&
        interactionZone.bounds.Contains(entry.player.bounds.center) && (exit == null || !exit.IsNear);
    public string Message => Time.unscaledTime < messageUntil ? "Thang máy chưa khả dụng" : "E — Gọi thang máy";
    private void Update()
    {
        if (CanInteract && entry.Gate.PlayerPressed("Interact")) messageUntil = Time.unscaledTime + 4;
    }
    private void OnGUI()
    {
        if (!CanInteract) return;
        if (style == null) style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true, normal = { textColor = Color.white } };
        style.fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height / 36f), 18, 36);
        var rect = new Rect(Screen.width * .2f, Screen.height * .78f, Screen.width * .6f, Screen.height * .14f);
        Color old = GUI.color; GUI.color = new Color(.025f, .04f, .05f, 1); GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = old;
        GUI.Label(rect, "THANG MÁY T3 — TẦNG TRỆT\n" + Message, style);
    }
}
