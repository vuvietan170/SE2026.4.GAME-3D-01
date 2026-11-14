
using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HUSEscape.Player
{
    [DisallowMultipleComponent, DefaultExecutionOrder(-8000)]
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        protected const string PlayerMapName = "Player";
        protected const string MoveActionName = "Move";
        protected const string SprintActionName = "Sprint";
        protected const string JumpActionName = "Jump";

        [Header("Input")]
        [SerializeField] protected InputActionAsset inputActions;

        [Header("Camera")]
        [SerializeField] protected Transform cameraTransform;

        [Header("Animation")]
        [SerializeField] protected Animator animator;
        [SerializeField, Min(0f)] protected float animationSmoothTime = 0.1f;

        [Header("Movement")]
        [SerializeField, Min(0f)] protected float moveSpeed = 4f;
        [SerializeField, Min(1f)] protected float sprintMultiplier = 1.6f;
        [SerializeField, Min(0f)] protected float rotationSharpness = 12f;
        [SerializeField, Min(0f)] protected float crouchSpeed = 2f;

        [Header("Jump and Gravity")]
        [SerializeField, Min(0f)] protected float jumpHeight = 1.2f;
        [SerializeField] protected float gravity = -20f;
        [SerializeField, Min(0f)] protected float groundedStickForce = 2f;

        protected static readonly int SpeedHash =
            Animator.StringToHash("Speed");
        protected static readonly int CrouchHash =
            Animator.StringToHash("IsCrouching");
        protected static readonly int JumpHash =
            Animator.StringToHash("Jump");
        protected static readonly int OpenHash =
            Animator.StringToHash("Open");
        protected static readonly int GrabHash =
            Animator.StringToHash("GrabTorch");

        protected CharacterController characterController;

        protected InputAction moveAction;
        protected InputAction sprintAction;
        protected InputAction jumpAction;

        protected GameplayInputGate inputGate;
        protected InputAction crouchAction;
        protected bool rejected;
        public bool IsCrouching => isCrouching;
        public GameplayInputGate InputGate => EnsureGate();
        protected GameplayInputGate EnsureGate()
        {
            if (inputGate == null) inputGate = GetComponent<GameplayInputGate>();
            if (inputGate == null) inputGate = gameObject.AddComponent<GameplayInputGate>();
            inputGate.Configure(inputActions, cameraTransform != null ? cameraTransform.GetComponent<ThirdPersonCameraFollow>() : null);
            return inputGate;
        }
        protected bool SelectMovementOwner()
        {
            foreach (var candidate in GetComponents<PlayerController>())
                if (candidate != this && candidate.isActiveAndEnabled && candidate.inputActions != null && candidate.GetEntityId().CompareTo(GetEntityId()) < 0)
                { rejected = true; enabled = false; Debug.LogWarning("Another controller already owns this CharacterController.", this); return false; }
            return true;
        }
        protected float verticalVelocity;
        protected bool isCrouching;

        protected virtual void Awake()
        {
            characterController = GetComponent<CharacterController>();
            if (!SelectMovementOwner()) return;

            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;

            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            if (animator != null)
                animator.applyRootMotion = false;
            else
                Debug.LogWarning(
                    "Player Animator not found.", this);

            EnsureGate();
            if (!TryResolveActions())
                enabled = false;
        }

        protected bool TryResolveActions()
        {
            if (inputActions == null)
            {
                Debug.LogError(
                    "Input Actions asset is missing.", this);
                return false;
            }

            try
            {
                var gate = EnsureGate();
                moveAction = gate.PlayerAction(MoveActionName);
                sprintAction = gate.PlayerAction(SprintActionName);
                jumpAction = gate.PlayerAction(JumpActionName);
                crouchAction = gate.PlayerAction("Crouch");
                if (moveAction == null || sprintAction == null || jumpAction == null || crouchAction == null)
                    throw new InvalidOperationException("Player actions Move/Sprint/Jump/Crouch are required.");
                return true;
            }
            catch (InvalidOperationException e)
            {
                Debug.LogError(e.Message, this);
                return false;
            }
        }

        protected virtual void OnEnable()
        {
            if (!SelectMovementOwner()) return;
            characterController = GetComponent<CharacterController>();
            if (!TryResolveActions()) enabled = false;
        }
        protected virtual void OnDisable()
        {
            verticalVelocity = 0f; isCrouching = false;
            if (!rejected && animator != null && animator.runtimeAnimatorController != null)
            { animator.SetFloat(SpeedHash, 0); animator.SetBool(CrouchHash, false); animator.ResetTrigger(JumpHash); }
        }

        protected virtual void Update()
        {
            if (inputGate == null || inputGate.IsBlocked || Time.deltaTime <= 0f) return;
            if (characterController == null || !characterController.enabled || moveAction == null)
                return;

            float dt = Time.deltaTime;

            // Crouch is a stance/speed toggle; the standing capsule deliberately stays unchanged.
            if (crouchAction.WasPressedThisFrame()) isCrouching = !isCrouching;

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

        protected Vector3 GetCameraRelativeDirection(Vector2 input)
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
                forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;

            if (right.sqrMagnitude < 0.0001f)
                right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;

            return Vector3.ClampMagnitude(
                right * input.x + forward * input.y, 1f);
        }

        protected void RotateTowards(Vector3 direction, float dt)
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

        protected virtual void OnValidate()
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
