using HUSEscape.World;
using UnityEngine;

/// <summary>Proposed MVP final stage, authored in the existing Campus rather than a new scene.</summary>
public sealed class MainGateExit : MonoBehaviour
{
    public JourneySceneEntry entry;
    [Min(.1f)] public float radius = 2.2f;
    public bool IsNear => entry != null && entry.player != null && Vector3.Distance(transform.position, entry.player.transform.position) < radius;
    private bool Available => IsNear && entry.Session != null && !entry.Session.IsLoading && !entry.Gate.IsBlocked;
    public bool TryUse() => entry != null && entry.Session != null && entry.Session.TryEscape(this);
    private void Update()
    {
        if (!Available || !entry.Gate.PlayerPressed("Interact")) return;
        if (!TryUse()) entry.ui.ShowInformation("CỔNG CHÍNH", entry.Session.LastMessage);
    }
    private void OnGUI()
    {
        if (Available) GUI.Box(new Rect(Screen.width * .18f, Screen.height * .82f, Screen.width * .64f, 65),
            entry.quests.OfficeIndex == 3 ? "E — Qua CỔNG CHÍNH / Hoàn thành hành trình" : "E — CỔNG CHÍNH: cần xác nhận tuyến đường tại Phòng Đào tạo");
    }
}
