# Floor 01 interiors and shared door IK

Scene: `Assets/_Project/Scenes/Floor01_Prototype.unity`.

## Saved authoring changes

A102 and LAB now have optional DoorHandleIK components, each with a separate
HandleTarget parented to its moving hinge. They use the existing right-arm rig,
with the same serialized tuning and local target offset as A101. A101's serialized
setup is unchanged. DoorHandleIK adds instance-owned mutual exclusion so idle
door components cannot reset another door's shared constraint. DoorInteraction
and the shared Door_Locked prefab asset are unchanged.

The new `Floor01_Interiors` hierarchy contains 329 objects, including grouping
transforms and UI labels, with 258 renderers and 117 colliders:

- A101: dress the four existing desks and add two student desks, twelve student
  chairs, a teacher desk/chair, writing board, notebooks/books, bookcase,
  decorative window panels and ceiling light fixtures.
- A102: the same student capacity, with a different teacher-desk position and a
  supply cabinet instead of A101's bookcase, plus writing board and supplies.
- LAB: retain the four original workstation desks/monitors, add four more,
  eight chairs, screens/keyboards/mice, teacher desk/terminal, rules board,
  decorative window panels and ceiling fixtures. The east-west central aisle
  at z=53 and the north-south aisle at x=17 remain open.
- Corridor: three benches, three bins, three potted plants, three notice boards,
  direction signs and visible ceiling fixtures. Furniture stays on the west
  side, away from the existing key stand and east-side door approaches.

Existing desks, architecture, lights and gameplay objects are retained. New
objects use existing Floor01 materials; no external assets, new materials,
packages, gameplay systems or runtime scene-generation scripts are added.
Structural furniture has colliders; notebooks, screens, labels, plant foliage,
window panels and high ceiling fixtures do not add unnecessary colliders.
Fixtures reuse the scene's existing illumination; they do not add realtime lights.

The original key positions and IDs are unchanged:
Key_A101 -> A101 -> Key_A102 -> A102 -> Key_Lab -> LAB.
Keys remain non-consumable. SampleScene, Player dimensions, camera settings,
Inventory/UI, model, Avatar and animation assets are untouched. Open speed is 2.2.

## Backup and verification artifacts

Before editing, the original scene and its metadata were copied to:
`C:/Users/ASUS/AppData/Local/Temp/HUS_Floor01_Interior_fb58a768068b4816a6c7a6bac600b14d/`.
This backup is outside Assets and is not imported or added to Build Settings.
Retain another durable copy if needed; the OS may clean temporary directories.

Authoring and native validation use the existing separate verification copy at:
`C:/Users/ASUS/AppData/Local/Temp/HUS_PlayerDuplicateFix_cba3350ccc274de684902c9ad701c1a4/`.
Temporary authoring scripts and input probes stay there, outside the repository.
The scene is saved on disk with normal editable objects, not generated in Play.

## Executed native validation

Unity 6000.6.5f1, separate verification copy, production gameplay and real virtual
keyboard input (not a replacement door/IK implementation):

- `floor-interior-Play.json`: **107/107 checks passed**, 0 failed, no captured
  runtime errors; Editor PID 21924 exited 0, no timeout.
- `floor-interior-regression-Test.json`: **33/33 checks passed**, 0 failed, no
  captured runtime errors; Editor PID 46844 exited 0, no timeout.
- All three locked doors reject E without starting Open or IK. Each accepted
  opening reaches the handle before rotation, follows the initial swing and
  releases the shared constraint/ownership afterward. The correct key is retained.
- Competing acquisition is rejected; enabling/disabling an idle door cannot
  reset the active owner's weight. Disabling the owner releases the reservation
  and restores culling, and another door can acquire it afterward.
- Actual WASD movement completes the original three-key route, physically enters
  every room and traverses both classroom row aisles and the LAB central aisle.
  Single E/F selection, collider/portal/hinge clearance and camera visibility at
  the sampled route waypoints pass. Inventory retains all three keys and Tab works.
- Walking, Shift sprint, Space jump/landing, Ctrl crouch/stand, F pickup and Tab
  work after IK. Repeated A101 close/open and walking away during approach release
  IK safely. Bone-length and hand/finger landmark checks also pass for A101.
- C# compilation succeeds in the verification copy. The final native-run logs
  contain no C# errors or new C# warnings. The existing unused `isJumping` CS0414
  warning appeared during the earlier full script rebuild and is not changed.

The delivered scene and DoorHandleIK.cs are hash-identical to those tested.
The original open Editor was not driven automatically. Static comparison of
the scene's existing serialized objects confirms no deletion: only A102/LAB
prefab-instance additions and SceneRoots change; A101 and all existing player,
camera, inventory, keys and architecture retain their serialized configuration.
SampleScene and all pre-existing metadata retain their original hashes.
The native reports and corresponding `.log`/`-process.json` files are in the
verification directory above. These are standalone Play Mode probes, not a claim
that the project's NUnit suites were run.

## Acceptance in the original Editor

Stop Play and allow script reload. If the scene was already open, reopen it from
disk after preserving any independent unsaved work. Open Floor01_Prototype and
expand Floor01_Interiors to inspect/edit the additions. Play from the existing
spawn, pick up Key_A101 using F, open A101 with E, collect Key_A102, then continue
through A102 to Key_Lab and LAB. Use Tab to inspect the existing inventory.

Visually inspect hand/palm/elbow alignment, contact release and camera views at
each door. Adjust only the relevant new door contact settings if necessary;
do not change A101 tuning or animation speeds. All three doors share one rig,
so a new door must reference that rig and its own moving HandleTarget; see
[DOOR_HAND_IK.md](DOOR_HAND_IK.md).

## Limitations

This remains a primitive-based prototype. Windows are decorative opaque panels
on the existing walls; no structural openings were cut. Chairs/computers are
static scenery. Plant foliage reuses the wall material instead of importing a
new foliage asset. Existing camera wall-clipping limitations remain unchanged.
Rendered hand-mesh penetration, finger grip quality, foot sliding, every camera
angle and actual FPS on the target machine require manual visual/performance
acceptance; transform, collider and waypoint checks do not prove those properties.
