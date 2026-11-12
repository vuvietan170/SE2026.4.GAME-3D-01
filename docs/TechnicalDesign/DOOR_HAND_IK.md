# Floor 01 right-hand door contact

This prototype adds optional Animation Rigging contact to Door_A101, Door_A102
and Door_LAB in `Assets/_Project/Scenes/Floor01_Prototype.unity`. SampleScene, the
Door_Locked prefab asset, model/Avatar imports, player/camera/inventory scripts,
and all eight animation assets remain unchanged. Open state speed stays **2.2**;
GrabTorch stays **2**. The Animator Controller and its IK Pass setting are not edited.

## Rig suitability and package

Unity 6000.6.5f1 reports the Asian_University_Student Avatar valid and Humanoid,
with Animator.isHuman=true. Its right chain is RightArm -> RightForeArm ->
RightHand, about 0.44025 m in total. No model bone is added, renamed, reparented,
rescaled, or reimported with different settings.

The Editor ships `com.unity.animation.rigging` **6.6.0** as a built-in package.
It was enabled explicitly in manifest.json and resolved/compiled with the existing
dependencies. packages-lock.json adds this package and changes Burst's dependency
depth from 2 to 1; its existing 2.0.0 version is retained. No rigging samples or
third-party assets are imported.

The setup follows Unity's [Two Bone IK constraint](https://docs.unity3d.com/Packages/com.unity.animation.rigging@1.4/manual/constraints/TwoBoneIKConstraint.html)
workflow, also linked by the installed package's HelpURL: Root/Mid/Tip plus a
Target and elbow Hint, blended using constraint weight.

## Saved scene setup

- Asian_University_Student's existing Animator GameObject gains RigBuilder.
- Its new A101_RightHandRig child has Rig, RightGripProxy, RightElbowHint and a
  RightArm_TwoBoneIK constraint referencing the existing Humanoid arm chain.
- The constraint starts at weight 0; the original FK animations remain the idle
  behavior. Position weight is 1 and rotation weight is 0, preserving Open's
  authored wrist orientation and finger animation.
- Each door has DoorHandleIK and its own HandleTarget child of the moving hinge.
  Its local position is `(1.5, -0.25, 0.24)`. This is a wrist target offset toward
  the corridor from the visible handle; the palm/fingers extend to the handle.
  Moving the hinge automatically moves the target. The fixed door frame is not
  its parent.
- References to the actual scene Animator, RigBuilder and constraint are assigned
  explicitly. No Singleton, global event bus, service locator, or player component
  replacement is introduced.

All three doors use the same existing RigBuilder and right-arm constraint; no
second arm rig is added. A102 and LAB copy A101's serialized timing and target
offset; A101's stance, pose and tuning are unchanged. The legacy
`A101_RightHandRig` hierarchy name is retained to preserve the existing setup.
Do not apply scene-specific actor references back into the shared Door_Locked
prefab asset.

For another door instance in this scene, add/copy DoorHandleIK, add a HandleTarget
under its moving hinge and assign the new target explicitly. Assign the existing
scene Animator, RigBuilder and right-arm constraint; retain that door's own key
ID and other DoorInteraction settings. Position the target locally at that door's
handle. Never duplicate the rig or store scene actor references in a prefab asset.
Doors without the optional component still use the original animation path.

## Lifecycle and timing

DoorInteraction's key check still runs first. A rejected key never starts Open,
alignment, or IK. After an accepted opening, it checks for the optional DoorHandleIK
component. Missing/disabled/invalid IK keeps the original interaction path.

DoorHandleIK uses the existing CharacterController to make a short,
collision-checked stance adjustment and face the door. This is needed because
the arm is only about 44 cm long; it cannot reach the handle from every point
within the original 1.6 m interaction radius. No movement speed, input binding,
CharacterController dimension, or player script is changed.

After alignment the existing Open trigger fires. IK ramps from 0 to Maximum
Weight over Approach Time. DoorInteraction keeps the original delay after the
trigger, then allows the configured approach plus a short contact grace period
to finish. The default setup reaches contact before the leaf starts rotating.

When the leaf begins moving, the target proxy follows HandleTarget. Hold Time
controls the initial follow window; Release Time blends back to FK. A separate
reach limit clamps the proxy and starts an early release before the hand is
pulled beyond the arm's reach. The solver rotates the original arm chain and
does not extend bone lengths. Closing the door uses the original non-IK path.

Animator culling is temporarily AlwaysAnimate only during this accepted contact
sequence, so the rig still evaluates if the renderer is offscreen. The previous
culling mode is restored on release, disable or destruction. Constraint weight
returns to zero. RigBuilder synchronization runs after the optional target update.

If the stance is too far away, movement is obstructed or contact is unreachable,
the component falls back safely to the existing door animation instead of
stretching the arm or permanently locking interaction. This is not a full-body
interaction system or automatic pathfinding. WASD is not rebound or disabled;
moving away during contact can trigger the reach safety release.

Before alignment, a DoorHandleIK instance reserves its shared constraint by
checking other live DoorHandleIK owners. Ownership is stored on the component,
without a static registry or Singleton. A competing request cannot write the
occupied rig. Only the owner can reset its weight and restore Animator culling;
enabling or disabling an idle door cannot clear another door's hand contact.
Release, disable and destruction relinquish ownership. Leaving arm reach during
approach also initiates release. The existing nearest-door selection and key
check remain responsible for accepting the E interaction itself.

## Inspector tuning

Select a door and its DoorHandleIK component (all three start with A101's values):

| Setting | Default | Meaning |
| --- | --- | --- |
| Approach Time | 0.25 s | Blend to the grip after the Open trigger. |
| Hold Time | 0.06 s | Follow the early swing before releasing; reach safety can shorten it. |
| Release Time | 0.18 s | Fade back to the unmodified animation. |
| Maximum Weight | 1 | Full contact at the calibrated target; low weights may not reach it. |
| Contact Tolerance | 0.06 m | Maximum wrist-to-target error accepted for contact. |
| Contact Grace Time | 0.2 s | Additional allowance after the approach/delay. |
| Alignment Time | 0.35 s | Smooth stance/rotation adjustment before the Open trigger. |
| Maximum Alignment Distance | 1.5 m | Reject larger automatic stance shifts. |
| Shoulder To Grip Distance | 0.2 m | Desired horizontal shoulder-to-target stance spacing. |
| Reach Limit | 0.95 | Fraction of total arm length used by the safety clamp. |

For visual calibration, move HandleTarget locally in small increments; do not
edit model bones or animation clips. The initial target is a wrist reference,
not the center of the hand mesh. Keep maximum weight 1 while calibrating contact.
Long holds may be cut short as the door moves away from this short arm.

## Initial A101 validation (before extension to A102 and LAB)

Tests ran in Unity 6000.6.5f1 using the existing separate verification copy, with
the same scene/runtime code and package versions delivered to the original
project. The original open Editor was not driven automatically.

- Focused native Play Mode: **30/30 checks passed**, exit 0, no timeout, no
  captured runtime errors. This uses real E input and the production components.
- Without the key, the door stays locked and neither Open nor IK starts.
- With the key, measured wrist-to-target error at opening is approximately
  0.000011 m at weight 1. Seven early-swing frames retain contact within 7 cm.
- The measured 0.44025 m arm length does not increase (about 0.000003 m numerical
  variation). 368 hand/finger joint samples show no penetration into the panel;
  the nearest finger landmark reaches the visible handle's bounds.
- Weight returns to zero and the original culling mode is restored. Walking,
  Shift sprint, Space jump/landing, Ctrl crouch/stand, F pickup and Tab inventory
  checks pass afterward. A102 and LAB keep their original opening/key behavior
  and have no DoorHandleIK component.
- The full Floor01 native navigation probe also passes **76/76 checks**, exit 0:
  the existing controller completes A101 -> A102 -> LAB, with saved colliders,
  keys, portal clearance and default camera checks intact.

Initial verification exposed headless Animator transform culling; scoped
AlwaysAnimate with restoration fixed evaluation during contact. Missing files
in the temporary package/input cache were restored from the original project;
the original input asset was not edited. Final C# compilation has no errors;
the pre-existing PlayerController1.isJumping CS0414 warning remains unchanged.
Reports, probe sources and logs remain outside the repository.

These are transform/joint and collider checks, not a rendered hand-mesh collision
or visual-quality approval. In the original Editor, stop Play, allow package/script
reload, open Floor01_Prototype and inspect A101 at a normal grip position. Obtain
Key_A101 and press E. Review palm orientation, finger silhouette, elbow and foot
alignment visually; adjust only the new contact/stance settings if needed. Do not
change Open speed 2.2 or the source skeleton. Existing camera wall-clipping limits
remain outside this feature.

## Three-door extension

See [FLOOR01_INTERIORS.md](FLOOR01_INTERIORS.md) for the current saved scene,
three-door integration validation, backup, layout and acceptance instructions.
