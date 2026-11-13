using HUSEscape.World;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Intentional nearby E entry/return, using the existing journey and production SceneLoader.</summary>
public sealed class TrainingOfficeEntrance : MonoBehaviour
{
    public JourneySceneEntry entry;
    public bool returnToCampus;
    public float radius = 1.8f;
    private string feedback;
    private float feedbackUntil;
    public bool IsNear => entry != null && entry.player != null && Vector3.Distance(transform.position, entry.player.transform.position) < radius;
    private bool Available => IsNear && entry.Session != null && !entry.Session.IsLoading && !entry.Gate.IsBlocked && !entry.player.GetComponent<InventoryUI>().IsOpen;
    private void Update()
    {
        if (Available && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            bool accepted = returnToCampus ? entry.Session.TryReturnCampus() : entry.Session.TryEnterOffice();
            if (!accepted) { feedback = entry.Session.LastMessage; feedbackUntil = Time.unscaledTime + 6; entry.ui.ShowInformation("LỐI VÀO PHÒNG ĐÀO TẠO", feedback); }
        }
    }
    private void OnGUI()
    {
        if (!Available) return;
        GUI.Box(new Rect(Screen.width*.2f, Screen.height*.82f, Screen.width*.6f, 65), Time.unscaledTime < feedbackUntil ? feedback : returnToCampus ? "E — Trở lại Campus" : entry.quests.CampusIndex < 3 ? "E — Phòng Đào tạo: cần hoàn tất khám phá Campus" : "E — Vào PHÒNG ĐÀO TẠO");
    }
}
