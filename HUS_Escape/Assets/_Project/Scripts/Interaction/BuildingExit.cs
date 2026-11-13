using HUSEscape.World;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>An intentional E-only exit; walking through its range never initiates travel.</summary>
public sealed class BuildingExit : MonoBehaviour
{
    [SerializeField] private JourneySceneEntry entry;
    [SerializeField] private float distance = 2.4f;
    [Tooltip("A visible exit door's local 3D approach area. A missing zone retains the legacy distance fallback.")]
    [SerializeField] private BoxCollider interactionZone;
    private string feedback;
    private float feedbackUntil;
    private bool feedbackWasComplete;
    private GUIStyle promptStyle;
    public bool IsNear => entry != null && entry.player != null && (interactionZone != null
        ? interactionZone.enabled && interactionZone.gameObject.activeInHierarchy && interactionZone.bounds.Contains(entry.player.bounds.center)
        : Vector3.Distance(transform.position, entry.player.bounds.center) < distance);
    public bool CanShowPrompt => IsNear && entry.Session != null && !entry.Session.IsLoading && !entry.Gate.IsBlocked && !entry.player.GetComponent<InventoryUI>().IsOpen;
    public string PromptText => Time.unscaledTime < feedbackUntil && feedbackWasComplete == entry.quests.IsComplete ? feedback : entry.quests.IsComplete
        ? "E — RỜI TÒA NHÀ T3 / ĐẾN CAMPUS"
        : "CỬA CHƯA MỞ — Hoàn thành đủ 17 mục tiêu tầng 1.";
    private void Update()
    {
        if (!CanShowPrompt) return;
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame && !entry.Session.TryLeaveFloor())
        { feedback = entry.Session.LastMessage; feedbackWasComplete = entry.quests.IsComplete; feedbackUntil = Time.unscaledTime + 8; }
    }
    private void OnGUI()
    {
        if (!CanShowPrompt) return;
        if (promptStyle == null) promptStyle = new GUIStyle(GUI.skin.label) { wordWrap = true, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
        promptStyle.fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height / 36f), 18, 36);
        Rect rect = new Rect(Screen.width * .2f, Screen.height * .77f, Screen.width * .6f, Screen.height * .17f);
        Color previous = GUI.color; GUI.color = new Color(.025f, .04f, .05f, 1);
        GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = entry.quests.IsComplete ? new Color(.2f, .9f, .55f) : new Color(1, .75f, .2f);
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 4), Texture2D.whiteTexture); GUI.color = previous;
        GUI.Label(new Rect(rect.x + 12, rect.y + 10, rect.width - 24, rect.height - 20), "LỐI RA T3 — TẦNG TRỆT\n" + PromptText, promptStyle);
    }
}
