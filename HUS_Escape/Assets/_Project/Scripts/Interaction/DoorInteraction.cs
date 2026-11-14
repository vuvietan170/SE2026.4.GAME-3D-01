
using System.Collections;
using UnityEngine;
using HUSEscape.Inventory;

public class DoorInteraction : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private Animator playerAnimator;

    [Header("Door Lock")]
    [SerializeField] private bool isLocked = true;
    [SerializeField] private string requiredKeyId = "Key01";
    [SerializeField] private bool consumeKey = false;

    [Header("Interaction")]
    [SerializeField] private float interactionDistance = 1.6f;
    [SerializeField] private float turnDuration = 0.2f;
    [SerializeField] private float doorOpenDelay = 0.6f;
    [SerializeField] private float actionCooldown = 1.2f;

    [Header("Door Movement")]
    [SerializeField] private float openAngle = 90f;
    [SerializeField] private float rotationSpeed = 60f;

    [Header("Message")]
    [SerializeField] private float messageDuration = 2f;

    private Quaternion closedRotation;
    private Quaternion openedRotation;

    private PlayerInventory inventory;
    private bool isOpen;
    private bool isInteracting;

    private string currentMessage = "";
    private float messageEndTime;
    public bool IsOpen => isOpen;
    public bool IsLocked => isLocked;
    public string RequiredKeyId => requiredKeyId;
    public bool IsBusy => isInteracting || Quaternion.Angle(transform.localRotation, isOpen ? openedRotation : closedRotation) > .2f;

    /// <summary>Applies validated persistence state without animation, key consumption or gameplay events.</summary>
    public void RestoreState(bool locked, bool open)
    {
        StopAllCoroutines(); isLocked = locked; isOpen = open; isInteracting = false;
        currentMessage = ""; messageEndTime = 0;
        transform.localRotation = open ? openedRotation : closedRotation;
    }

    private void Awake()
    {
        closedRotation = transform.localRotation;

        openedRotation = closedRotation *
            Quaternion.Euler(0f, openAngle, 0f);

        ResolvePlayer();
    }

    private void Update()
    {
        RotateDoor();

        if (IsSelectedTarget() && InteractionSelection.Pressed(player, "Interact") && !isInteracting)
        {
            TryInteract();
        }
    }

    private bool ResolvePlayer() => InteractionSelection.Resolve(this, ref player, ref playerAnimator, out inventory);

    private bool IsSelectedTarget()
    {
        if (!ResolvePlayer() || !IsPlayerNearDoor()) { return false; }
        // Busy/locked targets remain candidates: a press must not fall through to another door.
        return InteractionSelection.IsNearest(this, player,
            door => door.ResolvePlayer() && door.player == player && door.IsPlayerNearDoor(), true);
    }

    private void TryInteract()
    {
        if (isLocked)
        {
            if (inventory == null)
            {
                Debug.LogError(
                    "PlayerInventory chua duoc gan vao Player.");
                ShowMessage("Loi: chua co kho do!");
                return;
            }

            if (!inventory.HasItem(requiredKeyId))
            {
                ShowMessage("Cua da khoa! Can tim chia khoa.");
                return;
            }

            if (consumeKey)
                inventory.RemoveItem(requiredKeyId);

            isLocked = false;
            ShowMessage("Da mo khoa cua!");
        }

        StartCoroutine(InteractWithDoor());
    }

    private bool IsPlayerNearDoor()
    {
        Vector3 offset = player.position - transform.position;
        offset.y = 0f;

        return offset.sqrMagnitude <=
            interactionDistance * interactionDistance;
    }

    private IEnumerator InteractWithDoor()
    {
        isInteracting = true;

        if (!isOpen)
        {
            var handIK = GetComponent<DoorHandleIK>();
            if (handIK != null) { yield return handIK.AlignForOpening(player, playerAnimator); }
            bool useHandIK = handIK != null && handIK.IsPrepared;
            Vector3 direction =
                transform.position - player.position;

            direction.y = 0f;

            if (!useHandIK && direction.sqrMagnitude > 0.001f)
            {
                Quaternion startRotation = player.rotation;
                Quaternion targetRotation =
                    Quaternion.LookRotation(direction.normalized);

                float elapsed = 0f;
                float duration = Mathf.Max(0.01f, turnDuration);

                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;

                    float t = Mathf.Clamp01(elapsed / duration);
                    t = t * t * (3f - 2f * t);

                    player.rotation = Quaternion.Slerp(
                        startRotation,
                        targetRotation,
                        t
                    );

                    yield return null;
                }

                player.rotation = targetRotation;
            }

            if (playerAnimator != null)
            {
                playerAnimator.SetTrigger("Open");
            }

            if (useHandIK) { handIK.BeginReach(); }
            float remainingDelay =
                Mathf.Max(0f, doorOpenDelay - turnDuration);

            if (remainingDelay > 0f)
                yield return new WaitForSeconds(remainingDelay);

            if (useHandIK) { yield return handIK.WaitForContact(); }
            isOpen = true;
            if (useHandIK) { handIK.BeginFollowing(); }
        }
        else
        {
            isOpen = false;
        }

        yield return new WaitForSeconds(
            Mathf.Max(0f, actionCooldown));

        isInteracting = false;
    }

    private void RotateDoor()
    {
        Quaternion target =
            isOpen ? openedRotation : closedRotation;

        transform.localRotation = Quaternion.RotateTowards(
            transform.localRotation,
            target,
            rotationSpeed * Time.deltaTime
        );
    }

    private void ShowMessage(string message)
    {
        currentMessage = message;
        messageEndTime = Time.time + messageDuration;
    }

    private void OnGUI()
    {
        if (!IsSelectedTarget())
            return;

        string message = "";

        if (Time.time < messageEndTime)
        {
            message = currentMessage;
        }
        else if (!isInteracting)
        {
            message = isOpen
                ? "Nhan E de dong cua"
                : "Nhan E de mo cua";
        }

        if (string.IsNullOrEmpty(message))
            return;

        GUI.Box(
            new Rect(
                Screen.width / 2f - 160f,
                Screen.height - 120f,
                320f,
                45f
            ),
            message
        );
    }
}
