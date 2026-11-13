using System;
using HUSEscape.Inventory;
using UnityEngine;

/// <summary>Resolves scene-local actors and selects one nearby target without a global owner.</summary>
internal static class InteractionSelection
{
    internal static bool Resolve(Component target, ref Transform player,
        ref Animator animator, out PlayerInventory inventory)
    {
        inventory = null;
        if (player != null)
        {
            inventory = player.GetComponentInParent<PlayerInventory>();
            if (inventory == null) { inventory = player.GetComponentInChildren<PlayerInventory>(); }
        }
        else
        {
            // A prefab must not serialize a scene Player. Resolve only an unambiguous local actor.
            foreach (PlayerInventory candidate in UnityEngine.Object.FindObjectsByType<PlayerInventory>())
            {
                if (!candidate.isActiveAndEnabled || candidate.gameObject.scene != target.gameObject.scene) { continue; }
                if (inventory != null) { inventory = null; return false; }
                inventory = candidate;
            }
            if (inventory != null) { player = inventory.transform; }
        }

        if (inventory == null || !inventory.isActiveAndEnabled || player == null || !player.gameObject.activeInHierarchy) { return false; }
        if (inventory.TryGetComponent<HUSEscape.Player.GameplayInputGate>(out var gate) && gate.IsBlocked) { return false; }
        if (animator == null) { animator = inventory.GetComponentInChildren<Animator>(); }
        return true;
    }

    internal static bool IsNearest<T>(T target, Transform player, Func<T, bool> eligible, bool horizontal)
        where T : MonoBehaviour
    {
        T nearest = null;
        float bestDistance = float.PositiveInfinity;
        foreach (T candidate in UnityEngine.Object.FindObjectsByType<T>())
        {
            if (!candidate.isActiveAndEnabled || !eligible(candidate)) { continue; }
            Vector3 offset = candidate.transform.position - player.position;
            if (horizontal) { offset.y = 0f; }
            float distance = offset.sqrMagnitude;
            if (distance < bestDistance || (distance == bestDistance
                && nearest != null && candidate.GetEntityId().CompareTo(nearest.GetEntityId()) < 0))
            {
                nearest = candidate;
                bestDistance = distance;
            }
        }
        return nearest == target;
    }
}
