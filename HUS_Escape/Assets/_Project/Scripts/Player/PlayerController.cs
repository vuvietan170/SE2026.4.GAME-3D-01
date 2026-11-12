using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HUSEscape.Player
{
    /// <summary>Camera-relative third-person movement driven by the project's Player action map.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour
    {
        private const string PlayerMapName = "Player";
        private const string MoveActionName = "Move";
        private const string SprintActionName = "Sprint";
        private const string JumpActionName = "Jump";

        [Header("Input")]
        [SerializeField] private InputActionAsset inputActions;

        [Header("Camera")]
        [SerializeField] private Transform cameraTransform;

        [Header("Movement")]
        [SerializeField, Min(0f)] private float moveSpeed = 4f;
        [SerializeField, Min(1f)] private float sprintMultiplier = 1.6f;
        [SerializeField, Min(0f)] private float rotationSharpness = 12f;

        [Header("Jump and gravity")]
        [SerializeField, Min(0f)] private float jumpHeight = 1.2f;
        [SerializeField] private float gravity = -20f;
        [SerializeField, Min(0f)] private float groundedStickForce = 2f;

        private CharacterController characterController;
        private InputAction moveAction;
        private InputAction sprintAction;
        private InputAction jumpAction;
        private float verticalVelocity;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();

            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }

            if (!TryResolveActions())
            {
                enabled = false;
            }
        }

        private bool TryResolveActions()
        {
            if (inputActions == null)
            {
                Debug.LogError(
                    "PlayerController requires the InputSystem_Actions asset assigned to Input Actions.",
                    this);
                return false;
            }

            try
            {
                InputActionMap playerMap = inputActions.FindActionMap(PlayerMapName, true);
                moveAction = playerMap.FindAction(MoveActionName, true);
                sprintAction = playerMap.FindAction(SprintActionName, true);
                jumpAction = playerMap.FindAction(JumpActionName, true);
                return true;
            }
            catch (InvalidOperationException exception)
            {
                Debug.LogError(
                    $"PlayerController could not resolve the '{PlayerMapName}' action map " +
                    $"with '{MoveActionName}', '{SprintActionName}', and '{JumpActionName}' actions. " +
                    exception.Message,
                    this);
                return false;
            }
        }

        private void OnEnable()
        {
            if (moveAction == null || sprintAction == null || jumpAction == null)
            {
                return;
            }

            moveAction.Enable();
            sprintAction.Enable();
            jumpAction.Enable();
        }

        private void OnDisable()
        {
            moveAction?.Disable();
            sprintAction?.Disable();
            jumpAction?.Disable();
            verticalVelocity = 0f;
        }

        private void Update()
        {
            if (characterController == null || moveAction == null)
            {
                return;
            }

            float deltaTime = Time.deltaTime;
            Vector2 input = Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f);
            Vector3 moveDirection = GetCameraRelativeDirection(input);

            if (moveDirection.sqrMagnitude > 0.0001f)
            {
                RotateTowards(moveDirection, deltaTime);
            }

            bool grounded = characterController.isGrounded;
            if (grounded && verticalVelocity < 0f)
            {
                verticalVelocity = -groundedStickForce;
            }

            if (grounded && jumpAction != null && jumpAction.WasPressedThisFrame())
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }

            verticalVelocity += gravity * deltaTime;

            float speed = sprintAction != null && sprintAction.IsPressed()
                ? moveSpeed * sprintMultiplier
                : moveSpeed;

            Vector3 velocity = moveDirection * speed;
            velocity.y = verticalVelocity;
            characterController.Move(velocity * deltaTime);
        }

        private Vector3 GetCameraRelativeDirection(Vector2 input)
        {
            Transform reference = cameraTransform != null ? cameraTransform : transform;
            Vector3 forward = Vector3.ProjectOnPlane(reference.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(reference.right, Vector3.up).normalized;

            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            }

            if (right.sqrMagnitude < 0.0001f)
            {
                right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
            }

            return Vector3.ClampMagnitude(right * input.x + forward * input.y, 1f);
        }

        private void RotateTowards(Vector3 direction, float deltaTime)
        {
            if (rotationSharpness <= 0f)
            {
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
            float blend = 1f - Mathf.Exp(-rotationSharpness * deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, blend);
        }

        private void OnValidate()
        {
            moveSpeed = Mathf.Max(0f, moveSpeed);
            sprintMultiplier = Mathf.Max(1f, sprintMultiplier);
            rotationSharpness = Mathf.Max(0f, rotationSharpness);
            jumpHeight = Mathf.Max(0f, jumpHeight);
            groundedStickForce = Mathf.Max(0f, groundedStickForce);
            gravity = Mathf.Min(-0.01f, gravity);
        }
    }
}