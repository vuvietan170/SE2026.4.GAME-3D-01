
using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HUSEscape.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController1 : MonoBehaviour
    {
        private const string PlayerMapName = "Player";
        private const string MoveActionName = "Move";
        private const string SprintActionName = "Sprint";
        private const string JumpActionName = "Jump";

        [Header("Input")]
        [SerializeField] private InputActionAsset inputActions;

        [Header("Camera")]
        [SerializeField] private Transform cameraTransform;

        [Header("Animation")]
        [SerializeField] private Animator animator;
        [SerializeField, Min(0f)] private float animationSmoothTime = 0.1f;

        [Header("Movement")]
        [SerializeField, Min(0f)] private float moveSpeed = 4f;
        [SerializeField, Min(1f)] private float sprintMultiplier = 1.6f;
        [SerializeField, Min(0f)] private float rotationSharpness = 12f;
        [SerializeField, Min(0f)] private float crouchSpeed = 2f;

        [Header("Jump and Gravity")]
        [SerializeField, Min(0f)] private float jumpHeight = 1.2f;
        [SerializeField] private float gravity = -20f;
        [SerializeField, Min(0f)] private float groundedStickForce = 2f;

        private static readonly int SpeedHash =
            Animator.StringToHash("Speed");
        private static readonly int CrouchHash =
            Animator.StringToHash("IsCrouching");
        private static readonly int JumpHash =
            Animator.StringToHash("Jump");
        private static readonly int OpenHash =
            Animator.StringToHash("Open");
        private static readonly int GrabHash =
            Animator.StringToHash("GrabTorch");

        private CharacterController characterController;

        private InputAction moveAction;
        private InputAction sprintAction;
        private InputAction jumpAction;

        private float verticalVelocity;
        private bool isCrouching;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();

            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;

            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            if (animator != null)
                animator.applyRootMotion = false;
            else
                Debug.LogWarning(
                    "Player Animator not found.", this);

            if (!TryResolveActions())
                enabled = false;
        }

        private bool TryResolveActions()
        {
            if (inputActions == null)
            {
                Debug.LogError(
                    "Input Actions asset is missing.", this);
                return false;
            }

            try
            {
                InputActionMap playerMap =
                    inputActions.FindActionMap(PlayerMapName, true);

                moveAction =
                    playerMap.FindAction(MoveActionName, true);

                sprintAction =
                    playerMap.FindAction(SprintActionName, true);

                jumpAction =
                    playerMap.FindAction(JumpActionName, true);

                return true;
            }
            catch (InvalidOperationException e)
            {
                Debug.LogError(e.Message, this);
                return false;
            }
        }

        private void OnEnable()
        {
            moveAction?.Enable();
            sprintAction?.Enable();
            jumpAction?.Enable();
        }

        private void OnDisable()
        {
            moveAction?.Disable();
            sprintAction?.Disable();
            jumpAction?.Disable();

            verticalVelocity = 0f;
            isCrouching = false;
        }

        private void Update()
        {
            if (TryGetComponent<GameplayInputGate>(out var inputGate) && inputGate.IsBlocked)
                return;
            if (characterController == null || moveAction == null)
                return;

            float dt = Time.deltaTime;

            Keyboard keyboard = Keyboard.current;

            // CTRL: Toggle crouch
            if (keyboard != null &&
                (keyboard.leftCtrlKey.wasPressedThisFrame ||
                 keyboard.rightCtrlKey.wasPressedThisFrame))
            {
                isCrouching = !isCrouching;
            }

            Vector2 input = Vector2.ClampMagnitude(
                moveAction.ReadValue<Vector2>(), 1f);

            Vector3 direction = GetCameraRelativeDirection(input);

            bool moving = direction.sqrMagnitude > 0.0001f;

            bool sprinting =
                moving &&
                !isCrouching &&
                sprintAction != null &&
                sprintAction.IsPressed();

            if (moving)
                RotateTowards(direction, dt);

            bool grounded = characterController.isGrounded;

            if (grounded && verticalVelocity < 0f)
            {
                verticalVelocity = -groundedStickForce;
            }

            // SPACE: Jump
            if (grounded &&
                !isCrouching &&
                jumpAction != null &&
                jumpAction.WasPressedThisFrame())
            {
                verticalVelocity =
                    Mathf.Sqrt(jumpHeight * -2f * gravity);


                if (animator != null)
                    animator.SetTrigger(JumpHash);
            }

            verticalVelocity += gravity * dt;

            float actualSpeed = isCrouching
                ? crouchSpeed
                : sprinting
                    ? moveSpeed * sprintMultiplier
                    : moveSpeed;

            Vector3 velocity = direction * actualSpeed;
            velocity.y = verticalVelocity;

            CollisionFlags collision =
                characterController.Move(velocity * dt);

            if ((collision & CollisionFlags.Above) != 0 &&
                verticalVelocity > 0f)
            {
                verticalVelocity = 0f;
            }

            // Update Animator parameters
            if (animator != null)
            {
                float animationSpeed = !moving
                    ? 0f
                    : sprinting ? 1f : 0.4f;

                animator.SetFloat(
                    SpeedHash,
                    animationSpeed,
                    animationSmoothTime,
                    dt);

                animator.SetBool(CrouchHash, isCrouching);

             
            }
        }

        private Vector3 GetCameraRelativeDirection(Vector2 input)
        {
            Transform reference =
                cameraTransform != null
                    ? cameraTransform
                    : transform;

            Vector3 forward = Vector3.ProjectOnPlane(
                reference.forward, Vector3.up).normalized;

            Vector3 right = Vector3.ProjectOnPlane(
                reference.right, Vector3.up).normalized;

            if (forward.sqrMagnitude < 0.0001f)
                forward = transform.forward;

            if (right.sqrMagnitude < 0.0001f)
                right = transform.right;

            return Vector3.ClampMagnitude(
                right * input.x + forward * input.y, 1f);
        }

        private void RotateTowards(Vector3 direction, float dt)
        {
            if (rotationSharpness <= 0f)
                return;

            Quaternion target = Quaternion.LookRotation(
                direction, Vector3.up);

            float blend = 1f - Mathf.Exp(
                -rotationSharpness * dt);

            transform.rotation = Quaternion.Slerp(
                transform.rotation, target, blend);
        }

        private void OnValidate()
        {
            moveSpeed = Mathf.Max(0f, moveSpeed);
            crouchSpeed = Mathf.Max(0f, crouchSpeed);
            sprintMultiplier = Mathf.Max(1f, sprintMultiplier);
            rotationSharpness = Mathf.Max(0f, rotationSharpness);
            jumpHeight = Mathf.Max(0f, jumpHeight);
            groundedStickForce = Mathf.Max(0f, groundedStickForce);
            animationSmoothTime = Mathf.Max(0f, animationSmoothTime);
            gravity = Mathf.Min(-0.01f, gravity);
        }
    }
}
