# Player Foundation (FO-004A)

> Bản triển khai hiện tại: [Player input và pause thống nhất](PLAYER_INPUT_PAUSE.md).
> Phần dưới ghi lại thiết kế FO-004A ban đầu.

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