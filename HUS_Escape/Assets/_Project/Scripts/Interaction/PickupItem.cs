
using System.Collections;
using UnityEngine;
using HUSEscape.Inventory;

public class PickupItem : MonoBehaviour
{
    [Header("Item")]
    [SerializeField] private string itemId = "Key01";
    [SerializeField] private string itemName = "Chia khoa";

    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private Animator playerAnimator;

    [Header("Pickup Settings")]
    [SerializeField] private float interactionDistance = 1.5f;
    [SerializeField] private float pickupDelay = 0.6f;

    private PlayerInventory inventory;
    private bool isPickingUp;
    private bool collected;
    public string ItemId => itemId;
    public bool IsCollected => collected;
    public bool IsBusy => isPickingUp && !collected;

    /// <summary>Restores a validated pickup flag without triggering animation or adding inventory items.</summary>
    public void RestoreState(bool wasCollected, bool available)
    {
        StopAllCoroutines(); collected = wasCollected; isPickingUp = false;
        gameObject.SetActive(available && !collected);
    }

    private void Start()
    {
        ResolvePlayer();
    }

    private void Update()
    {
        if (IsSelectedTarget() && InteractionSelection.Pressed(player, "Pickup")
            && !collected && !isPickingUp)
        {
            StartCoroutine(Pickup());
        }
    }

    private bool ResolvePlayer() => InteractionSelection.Resolve(this, ref player, ref playerAnimator, out inventory);

    private bool IsSelectedTarget()
    {
        if (!ResolvePlayer() || !IsNearPlayer()) { return false; }
        return InteractionSelection.IsNearest(this, player,
            item => !item.collected && item.ResolvePlayer() && item.player == player && item.IsNearPlayer(), false);
    }

    private bool IsNearPlayer()
    {
        Vector3 distance = player.position - transform.position;

        return distance.sqrMagnitude <=
            interactionDistance * interactionDistance;
    }

    private IEnumerator Pickup()
    {
        if (inventory == null)
        {
            Debug.LogError(
                "PlayerInventory chưa được gắn vào Player.");
            yield break;
        }

        isPickingUp = true;

        if (playerAnimator != null)
            playerAnimator.SetTrigger("GrabTorch");

        yield return new WaitForSeconds(pickupDelay);

        if (inventory.AddItem(itemId))
        {
            collected = true;
            Debug.Log("Đã thêm vào kho: " + itemName);
            gameObject.SetActive(false);
        }
        else
        {
            Debug.LogWarning(
                "Vật phẩm đã có trong kho: " + itemId);
            isPickingUp = false;
        }
    }

    private void OnGUI()
    {
        if (collected || isPickingUp || !IsSelectedTarget())
            return;

        GUI.Box(
            new Rect(
                Screen.width / 2f - 120f,
                Screen.height - 160f,
                240f,
                45f
            ),
            "Nhan F de nhat: " + itemName
        );
    }
}
