
using System.Collections.Generic;
using UnityEngine;

namespace HUSEscape.Inventory
{
    public class PlayerInventory : MonoBehaviour
    {
        [SerializeField] private int capacity = 12;

        private readonly HashSet<string> items = new();
        private readonly List<string> itemOrder = new();

        public IReadOnlyList<string> ItemIds => itemOrder;
        public int Capacity => capacity;

        /// <summary>Restores validated save data silently, without pickup rewards or duplicate logs.</summary>
        public void RestoreItems(IReadOnlyList<string> restored)
        {
            items.Clear(); itemOrder.Clear();
            foreach (string id in restored) { items.Add(id); itemOrder.Add(id); }
        }

        public bool AddItem(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                return false;

            if (items.Contains(itemId))
                return false;

            if (itemOrder.Count >= capacity)
            {
                Debug.Log("Tui do da day!");
                return false;
            }

            items.Add(itemId);
            itemOrder.Add(itemId);

            Debug.Log("Da nhat: " + itemId);
            return true;
        }

        public bool HasItem(string itemId)
        {
            return items.Contains(itemId);
        }

        public bool RemoveItem(string itemId)
        {
            if (!items.Remove(itemId))
                return false;

            itemOrder.Remove(itemId);
            return true;
        }
    }
}
