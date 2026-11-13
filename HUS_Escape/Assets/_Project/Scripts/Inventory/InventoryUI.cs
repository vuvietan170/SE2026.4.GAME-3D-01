
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using HUSEscape.Inventory;

public class InventoryUI : MonoBehaviour
{
    [Serializable]
    public class ItemVisual
    {
        public string itemId;
        public string displayName;
        [TextArea] public string description;
        public Texture2D icon;
    }

    [Header("References")]
    [SerializeField] private PlayerInventory inventory;

    [Header("Item Display Data")]
    [SerializeField] private List<ItemVisual> itemVisuals = new();

    private bool isOpen;
    public bool IsOpen => isOpen;

    private const int Columns = 4;
    private const int Rows = 3;

    private void Awake()
    {
        if (inventory == null)
            inventory = GetComponent<PlayerInventory>();
    }

    private void Update()
    {
        if (TryGetComponent<HUSEscape.Player.GameplayInputGate>(out var gate) && gate.IsBlocked) return;
        if (Keyboard.current != null &&
            Keyboard.current.tabKey.wasPressedThisFrame)
        {
            isOpen = !isOpen;
        }
    }

    private ItemVisual FindVisual(string itemId)
    {
        foreach (ItemVisual visual in itemVisuals)
        {
            if (visual.itemId == itemId)
                return visual;
        }

        return null;
    }

    private void OnGUI()
    {
        if (TryGetComponent<HUSEscape.Player.GameplayInputGate>(out var gate) && gate.IsBlocked) return;
        if (!isOpen || inventory == null)
            return;

        float width = Mathf.Min(520f, Screen.width - 24f);
        float height = 420f;
        float x = (Screen.width - width) / 2f;
        float y = Mathf.Max(8f, (Screen.height - height) / 2f);

        Rect panel = new Rect(x, y, width, height);

        Color previousColor = GUI.color;
        GUI.color = new Color(0.10f, 0.11f, 0.14f, 0.96f);
        GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUI.color = previousColor;

        GUI.Box(panel, "");

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.fontSize = 21;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.alignment = TextAnchor.MiddleCenter;

        GUI.Label(
            new Rect(x + 20f, y + 14f, width - 40f, 35f),
            "INVENTORY",
            titleStyle
        );

        GUI.Label(
            new Rect(x + 24f, y + 53f, width - 48f, 24f),
            $"Vat pham: {inventory.ItemIds.Count}/12"
        );

        float gap = 10f;
        float gridWidth = width - 48f;
        float slotWidth = (gridWidth - gap * 3f) / Columns;
        float slotHeight = 75f;
        float startX = x + 24f;
        float startY = y + 86f;

        string hoverName = "";
        string hoverDescription = "";

        for (int i = 0; i < Columns * Rows; i++)
        {
            int col = i % Columns;
            int row = i / Columns;

            Rect slot = new Rect(
                startX + col * (slotWidth + gap),
                startY + row * (slotHeight + gap),
                slotWidth,
                slotHeight
            );

            GUI.color = new Color(0.23f, 0.25f, 0.29f, 1f);
            GUI.Box(slot, "");
            GUI.color = previousColor;

            if (i >= inventory.ItemIds.Count)
                continue;

            string itemId = inventory.ItemIds[i];
            ItemVisual visual = FindVisual(itemId);

            string displayName =
                visual != null &&
                !string.IsNullOrWhiteSpace(visual.displayName)
                    ? visual.displayName
                    : itemId;

            string description =
                visual != null ? visual.description : "";

            if (visual != null && visual.icon != null)
            {
                Rect iconRect = new Rect(
                    slot.x + (slot.width - 36f) / 2f,
                    slot.y + 6f,
                    36f,
                    36f
                );

                GUI.DrawTexture(
                    iconRect,
                    visual.icon,
                    ScaleMode.ScaleToFit
                );
            }

            GUIStyle nameStyle = new GUIStyle(GUI.skin.label);
            nameStyle.alignment = TextAnchor.MiddleCenter;
            nameStyle.fontSize = 11;

            GUI.Label(
                new Rect(
                    slot.x + 2f,
                    slot.y + 46f,
                    slot.width - 4f,
                    22f
                ),
                displayName,
                nameStyle
            );

            if (slot.Contains(Event.current.mousePosition))
            {
                hoverName = displayName;
                hoverDescription = description;
            }
        }

        float detailsY = startY + Rows * (slotHeight + gap) + 2f;

        GUIStyle detailStyle = new GUIStyle(GUI.skin.label);
        detailStyle.wordWrap = true;
        detailStyle.fontSize = 12;

        string details = string.IsNullOrEmpty(hoverName)
            ? "Di chuot vao vat pham de xem thong tin."
            : hoverName + "\n" + hoverDescription;

        GUI.Label(
            new Rect(x + 24f, detailsY, width - 48f, 48f),
            details,
            detailStyle
        );

        GUI.Label(
            new Rect(x + 24f, y + height - 26f, width - 48f, 20f),
            "Nhan TAB de dong tui do."
        );
    }
}
