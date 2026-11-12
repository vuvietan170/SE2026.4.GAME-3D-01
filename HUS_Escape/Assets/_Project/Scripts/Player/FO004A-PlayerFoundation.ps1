$ErrorActionPreference = "Stop"

function Invoke-Git {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)
    & git @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Git command failed: git $($Arguments -join ' ')"
    }
}

$repoRoot = (Get-Location).Path
$branch = (& git branch --show-current).Trim()
if ($LASTEXITCODE -ne 0) { throw "Cannot read the current Git branch." }

$expectedBranch = "feature/fo-004a-player-foundation"
if ($branch -ne $expectedBranch) {
    throw "Wrong branch '$branch'. Switch to '$expectedBranch' before running this script."
}

$status = & git status --porcelain
if ($LASTEXITCODE -ne 0) { throw "Cannot read Git status." }
if ($status) {
    throw "Working tree is not clean. This script stopped to protect existing changes:`n$status"
}

$playerDir = Join-Path $repoRoot "HUS_Escape/Assets/_Project/Scripts/Player"
$scriptPath = Join-Path $playerDir "PlayerController.cs"
$asmdefPath = Join-Path $playerDir "HUSEscape.Player.asmdef"
$scriptMetaPath = "$scriptPath.meta"
$asmdefMetaPath = "$asmdefPath.meta"
$keepPath = Join-Path $playerDir ".gitkeep"
$docPath = Join-Path $repoRoot "docs/TechnicalDesign/PLAYER_FOUNDATION.md"

foreach ($path in @($scriptPath, $asmdefPath, $scriptMetaPath, $asmdefMetaPath, $docPath)) {
    if (Test-Path $path) { throw "Target already exists; nothing was changed: $path" }
}

function Write-Utf8NoBom {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Content
    )
    $parent = Split-Path -Parent $Path
    if (-not (Test-Path $parent)) {
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
    }
    [System.IO.File]::WriteAllText($Path, $Content, [System.Text.UTF8Encoding]::new($false))
}

$controller = @'
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
'@

$asmdef = @'
{
    "name": "HUSEscape.Player",
    "rootNamespace": "HUSEscape.Player",
    "references": [
        "Unity.InputSystem"
    ]
}
'@

$doc = @'
# Player Foundation (FO-004A)

## Scope

FO-004A introduces the first player movement component in the `HUSEscape.Player`
assembly. It is deliberately independent of `HUSEscape.Core` and `HUSEscape.UI`.

`PlayerController` uses Unity's `CharacterController` and the existing
`Assets/InputSystem_Actions.inputactions` asset. It resolves only the `Player`
action map actions `Move`, `Sprint`, and `Jump`; unrelated actions such as
`Attack`, `Interact`, and `Look` are not enabled by this component.

## Controls

| Action | Default control |
| --- | --- |
| Move | WASD or arrow keys |
| Sprint | Left Shift |
| Jump | Space |

Movement is camera-relative on the horizontal plane. The character smoothly
turns toward the direction of travel. If no camera is assigned, the component
uses the `MainCamera` transform when available, then falls back to the player's
own forward direction.

## Unity setup

1. Add a `CharacterController` to the player GameObject.
2. Add `PlayerController` to the same GameObject.
3. Assign `Assets/InputSystem_Actions.inputactions` to **Input Actions**.
4. Assign the active gameplay camera transform to **Camera Transform**. The
   `MainCamera` tag is used as a fallback when this field is empty.
5. Adjust the CharacterController capsule and movement values in the Inspector.

The component disables itself and logs a clear error if the Input Actions asset
or any required action is missing. It does not create a player object, camera,
scene, prefab, or input asset automatically.

## Boundaries

This foundation does not implement camera orbit/look, animation, footsteps,
interaction, combat, inventory, checkpoints, or networking. The existing `Look`
action is reserved for a later camera feature. Gravity and jumping are handled
through `CharacterController.Move`; no Rigidbody is required.

## Validation

After Unity imports the new assembly and script, verify compilation in the
Editor, then test walking, diagonal movement, sprinting, jumping, landing, and
camera-relative movement in a gameplay scene. Automated Unity tests have not
been run as part of this source addition.
'@

$scriptGuid = [Guid]::NewGuid().ToString("N")
$asmdefGuid = [Guid]::NewGuid().ToString("N")

$scriptMeta = @"
fileFormatVersion: 2
guid: $scriptGuid
MonoImporter:
  externalObjects: {}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {instanceID: 0}
  userData:
  assetBundleName:
  assetBundleVariant:
"@

$asmdefMeta = @"
fileFormatVersion: 2
guid: $asmdefGuid
AssemblyDefinitionImporter:
  externalObjects: {}
  userData:
  assetBundleName:
  assetBundleVariant:
"@

Write-Utf8NoBom -Path $scriptPath -Content $controller
Write-Utf8NoBom -Path $asmdefPath -Content $asmdef
Write-Utf8NoBom -Path $scriptMetaPath -Content $scriptMeta
Write-Utf8NoBom -Path $asmdefMetaPath -Content $asmdefMeta
Write-Utf8NoBom -Path $docPath -Content $doc

if (Test-Path $keepPath) {
    Remove-Item -LiteralPath $keepPath
}

Invoke-Git -Arguments @("add", "--",
    "HUS_Escape/Assets/_Project/Scripts/Player/PlayerController.cs",
    "HUS_Escape/Assets/_Project/Scripts/Player/PlayerController.cs.meta",
    "HUS_Escape/Assets/_Project/Scripts/Player/HUSEscape.Player.asmdef",
    "HUS_Escape/Assets/_Project/Scripts/Player/HUSEscape.Player.asmdef.meta",
    "HUS_Escape/Assets/_Project/Scripts/Player/.gitkeep",
    "docs/TechnicalDesign/PLAYER_FOUNDATION.md"
)

Invoke-Git -Arguments @("diff", "--cached", "--check")
Invoke-Git -Arguments @("commit", "-m", "feat(player): add camera-relative movement foundation")
Invoke-Git -Arguments @("push", "-u", "origin", $expectedBranch)

Write-Host ""
Write-Host "FO-004A Player Foundation committed and pushed."
Write-Host "Next: open HUS_Escape in Unity 6000.6.5f1 and verify compilation."
Invoke-Git -Arguments @("status", "-sb")
