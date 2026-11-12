# Third-person indoor camera collision

## Scope and preserved behavior

The existing `HUSEscape.Player.ThirdPersonCameraFollow` component is extended in
`Assets/_Project/Scripts/Player/ThirdPersonCameraFollow.cs`. Its existing script
GUID `90ac04bfc790fc94e81b6b12155ebfdd` and scene references are retained.
No second camera, Cinemachine package, player controller or scene asset is added.

Existing settings remain: distance 6.3, sensitivity 0.12, pitch -20 to 65,
look-at offset (0, 1.2, 0), position smoothing 0.08 and rotation sharpness 12.
The same Player/Look action drives yaw/pitch; FOV and near clip are unchanged.
The original world-position SmoothDamp and rotation Slerp are retained.

No Scene/Prefab, PlayerController, Animator/Avatar, Mixamo clip, Inventory/UI,
DoorInteraction, DoorHandleIK, key ID, Input Actions, Package or ProjectSettings
file is edited. The map, interior geometry, door hinges and IK targets are kept.

## Collision path

LateUpdate synchronizes Transform-driven doors with physics, then sphere-casts
from the target focus toward the requested orbit position. The nearest included
solid collider limits the distance with a small surface margin. Obstruction can
shorten the boom immediately; extension uses SmoothDamp to avoid a sudden return
to full distance. Unobstructed movement keeps the original orbit behavior.

The smoothed candidate is checked again so the smoothing path cannot leave the
camera beyond a wall or cut a corner. Overlap resolution handles cases that
sphere casts cannot detect, including a moving door enclosing the camera or a
cast beginning inside a collider. The overlap solver uses up to eight passes;
the final line check preserves clearance toward the focus.

The query radius is at least Collision Radius and automatically encloses all
four near-plane corners, including their forward offset, using the current
projection/aspect. This conservative sphere protects the view edges while the
original camera rotation is still smoothing. It can pull the camera in earlier
than a simple ray or center-only probe would.

Non-allocating query buffers are reused and grow only when saturated, so ignored
Player hits cannot silently hide a nearer environment hit. The overlap shape is
one disabled, hidden, unsaved SphereCollider added to the existing Camera object
at runtime, used only by ComputePenetration. It never participates in physics
simulation, is not saved in the scene and is destroyed with the camera component.

## Inspector

On Main Camera -> ThirdPersonCameraFollow -> Environment collision:

| Field | Default | Purpose |
| --- | --- | --- |
| Collision Layers | Everything | Select which solid environment layers block the camera. |
| Collision Radius | 0.2 m | Minimum radius; actual radius grows to cover the near plane. |
| Collision Skin | 0.03 m | Extra clearance from hit surfaces. |
| Collision Recovery Time | 0.18 s | Smoothing when extending after an obstruction clears. |

Player descendants and Camera descendants are excluded by Transform hierarchy
even if they share the Default layer with walls. Trigger colliders are always
ignored with QueryTriggerInteraction.Ignore. The mask can exclude additional
existing layers; this task does not reassign scene layers or change TagManager.
No manual component attachment or reference rebinding is required.

## Backup and native validation

The original camera script, its metadata, Floor01_Prototype scene and scene
metadata were backed up before editing at:
`C:/Users/ASUS/AppData/Local/Temp/HUS_CameraCollision_214e083815824cf89907ecfc7a249c58/`.
Keep a durable copy if needed; OS temporary directories may be cleaned. Do not
import the backup scene into Assets because it retains the original GUID.

Native Unity 6000.6.5f1 validation used the existing separate verification copy:
`C:/Users/ASUS/AppData/Local/Temp/HUS_PlayerDuplicateFix_cba3350ccc274de684902c9ad701c1a4/`.
Its production assets/settings/packages were synchronized from the current
project before testing. No original Editor session was automated or terminated.
Temporary probes/geometry stay outside the repository and are never saved into
Floor01_Prototype. They exercise the production camera component and physics.

| Native report | Result | Process |
| --- | --- | --- |
| camera-collision-Play.json | 32/32 pass, 0 failures/runtime errors | PID 38744, exit 0, no timeout |
| camera-route-Play.json | 107/107 pass, 0 failures/runtime errors | PID 54592, exit 0, no timeout |
| camera-regression-Test.json | 33/33 pass, 0 failures/runtime errors | PID 25540, exit 0, no timeout |

Camera checks cover the corridor's original 6.3 m distance, actual mouse yaw/pitch
sensitivity, west-wall retraction, stationary jitter and smooth recovery; full
yaw/pitch sweeps in A101/A102/LAB; furniture/computers; real Space jump below the
ceiling; real E opening and a nearby moving door; thin walls; ignored triggers,
LayerMask exclusion/reinclusion, Player descendants, saturated hit buffers,
obstacles enclosing the camera and an overlapped cast origin. Room-corner checks
include 4:3, 16:9 and 21:9 aspect ratios.

Across 3,178 monitored LateUpdate frames, measured sphere penetration is zero
and no tested near-plane corner enters an included obstacle. Stationary jitter
after settling is zero in the sampled wall test. The jumping camera's highest
center is about 4.9565 m with an effective 0.4163 m radius, below the 5.4 m ceiling
with clearance. These measurements describe tested cases, not every possible
future collider arrangement.

The full production route picks up all three keys, opens all doors with the
matching non-consumable keys and keeps E/F/Tab, single-target selection and IK
working. Walking, Shift sprint, Space jump/landing, Ctrl crouch/stand, pickup,
inventory and repeated door/IK recovery pass. These are standalone native Play
Mode probes, not a claim that NUnit suites were run.

C# compilation succeeds in the verification copy. Final logs contain no new C#
warnings or errors; the pre-existing PlayerController1.isJumping CS0414 warning
remains untouched. Reports, `.log` and `-process.json` files are in that same
verification directory. The delivered script matches the tested script bytes.

Final hash comparison against the pre-task snapshot finds exactly one modified
file (ThirdPersonCameraFollow.cs), one new file (this document), and no deletions.
All scene, prefab, material, existing metadata, gameplay and configuration files
retain their hashes. No duplicate asset GUIDs are found. The two task files have
valid UTF-8 and no trailing whitespace. `git diff --check` still reports the same
seven pre-existing whitespace lines in unchanged SampleScene; they are not fixed
outside scope. No staging, commit, push, merge or branch switch is performed.

## Acceptance and limits

Stop Play, wait for script reload, then open Floor01_Prototype normally. Try
walking along corridor walls, orbiting each room, standing near an open door,
moving around desks/computers and jumping while looking upward. No Scene reload
or prefab modification is needed to attach the new behavior.

The original Game View has not been manually reviewed for every angle or for
subjective camera feel. Runtime FPS/profiling remains unmeasured. An immediate
inward safety correction can be visible if a collider appears around the camera
or the target teleports; smoothing that correction through the wall would clip.
Very tight spaces can bring the camera close to the character. This does not add
character fading, shoulder switching, automatic navigation or collision for
decorations that have no collider. Layer-excluded objects intentionally do not
block the camera. Impossible spaces smaller than the probe require level/layout
review rather than a guarantee from this local camera solver.
