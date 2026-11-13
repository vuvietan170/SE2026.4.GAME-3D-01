# Quest V3 — restore the LAB computer system

Stage 11 extends only LAB gameplay in Floor01_Prototype. Work remains on
`feature/fo-005-quest-v1`; all uncommitted V1/V2 work is retained. No branch switch,
staging, reset, clean, deletion, commit, push, merge or pull request was performed.

## Gameplay and progression

The first 12 accepted quest definitions remain unchanged. Five objectives are
appended to the existing QuestManager, making 17 in total:

| Objective | Gameplay fact |
| --- | --- |
| Inspect the central computer | `lab:inspect` |
| Restore power at the breaker | `lab:power` |
| Connect physical devices A -> B -> C | `lab:connected` |
| Access data on the ready computer | `lab:data` |
| Read exit guidance and confirm floor completion | `lab:complete` |

Entering LAB through the existing Key_Lab door/room trigger advances from the
old LAB-entry objective to the new computer objective. No QuestManager code or
second manager is introduced.

1. Press E at the central computer. It reports missing power.
2. Press E at the LAB breaker to restore power. Attempts before inspecting the
   computer give guidance and do not skip that objective. Repeated breaker
   interaction is harmless; power remains on.
3. Return to the computer and press E to read the connection instructions.
4. Walk to A (backup power), B (controller), then C (data server), pressing E at
   each physical device. Device interaction before power or before reading the
   instructions is rejected with feedback.
5. Wrong order resets all connection progress and device indicators, retaining
   power/instructions and all inventory items. Restart from A; no hard reset or
   new item is needed. Already-complete connections are not reset by extra presses.
6. After A-B-C, all device indicators and the computer show ready green. Return
   to the computer and press E to access the data.
7. The data dialog directs the player back through the open LAB door to the
   floor-1 corridor. It explicitly states that later areas are not implemented.
   Confirm with Enter or the completion button to finish all 17 objectives.

Esc closes a status/data dialog. Closing data with Esc does not falsely mark
its acknowledgement complete; E at the computer reopens it for confirmation.
Once completed, the computer displays the final success state instead of asking
for another confirmation. J shows the final journal; Tab remains the same
12-slot Inventory. No Campus/Training Office/Main Gate content is created.

## Components and UI reuse

- `LabRestoreSystem` owns only the scene-local LAB state: power, instructions
  read, connected count, readiness, data access and completion. Existing
  QuestManager.Signal/HasObserved APIs report/verify gameplay facts. No inventory
  storage or global service/event system is added.
- `LabInteractable` identifies Computer, Breaker or DeviceA/B/C and calls the
  existing InteractionSelection.IsNearest helper to select one eligible LAB
  target. Scene, active state and interaction distance are checked. Targets are
  away from the room door; UI gate acquisition occurs before legacy door/item
  Update, preventing the same E/F press from acting behind a dialog.
- `QuestPuzzleUI` appends a Lab mode, preserving existing enum values and
  PIN/sequence/clue/journal behavior. It reuses the same modal panel, message
  area, Esc/Enter behavior and GameplayInputGate. Enter closes ordinary status
  dialogs; on data guidance it confirms completion. Mouse buttons call the same
  completion method. No new UI framework or input map is introduced.
- Indicators use MaterialPropertyBlock on five explicitly assigned new Renderers
  (screen, breaker and three devices). No shared material asset or instantiated
  material is modified. MaterialPropertyBlock is created in Awake, not during
  serialization or a MonoBehaviour constructor.

State colors: computer dark while unpowered, amber while powered/incomplete,
green when ready; breaker red/off or green/on; devices gray/disconnected or
green/connected. Wrong order returns all devices to gray. Native tests inspect
the actual renderer property blocks; rendered visual acceptance remains separate.

## Saved Scene layout

Scene: `HUS_Escape/Assets/_Project/Scenes/Floor01_Prototype.unity`.
New objects live under `Floor01_QuestV3_LAB`:

| Object | World position / use |
| --- | --- |
| Existing Teacher_Terminal | (19, 1.1, 47.4), gains only a LabInteractable component; no transform/material change |
| Central status screen/label | Added in front of the existing teacher monitor |
| LAB_Breaker | (7.3, 1.4, 48), inside the west wall, away from the door swing |
| LAB_Device_A | (9.4, 0.75, 58.6), backup power |
| LAB_Device_B | (22.2, 0.75, 53), controller |
| LAB_Device_C | (22.2, 1, 58.6), data server |

The additions are 20 editable GameObjects including grouping/labels, nine new
MeshRenderers, four solid colliders and five world-space labels. There are five
physical interaction components, one on the reused computer and four on new
objects. No new materials, textures, complex meshes, lights or physics simulation
are added. The computer is reachable from its west side in the existing central
aisle; its interaction radius is 1.9 m. The four new targets use 1.6 m.

Original room dimensions, furniture, Player spawn, camera configuration, room
door hinges/IK targets, keys, A101 cabinet and A102 compartment remain unchanged.
Existing travel aisles remain usable. The new B/C devices were moved 0.6 m east
from their initial prototype positions after native movement exposed a narrow
approach; accepted desks and architecture were not moved.

Static comparison changes only three existing serialized Scene blocks: the
QuestManager definition list, Teacher_Terminal's component list and SceneRoots.
There are 83 new serialized blocks and no deleted block. The original manager
block is retained as a prefix, proving the first 12 definitions are untouched.
Extra native serialization of camera/A101 defaults was excluded from delivery.
No temporary validation probe is saved in the Scene.

## Exact stage-11 file scope

Modified (2):

- `HUS_Escape/Assets/_Project/Scenes/Floor01_Prototype.unity`
- `HUS_Escape/Assets/_Project/Scripts/Puzzle/QuestPuzzleUI.cs`

New (9):

- `HUS_Escape/Assets/_Project/Scripts/Puzzle/LabRestoreSystem.cs`
- `HUS_Escape/Assets/_Project/Scripts/Puzzle/LabRestoreSystem.cs.meta`
- `HUS_Escape/Assets/_Project/Scripts/Puzzle/LabInteractable.cs`
- `HUS_Escape/Assets/_Project/Scripts/Puzzle/LabInteractable.cs.meta`
- `tools/QuestV3/QuestV3Setup.cs`
- `tools/QuestV3/QuestV3PlayProbe.cs`
- `tools/QuestV3/QuestV3PlayValidation.cs`
- `tools/QuestV3/Run-QuestV3.ps1`
- `docs/TechnicalDesign/QUEST_V3_LAB.md` (this document).

All pre-stage-11 files outside the two modified paths retain their hashes,
including every V1/V2 runtime file/tool except the intentionally extended UI,
PlayerController, GameplayInputGate, Inventory/UI, InteractionSelection, camera,
Animator/Avatar/model/eight Mixamo clips, materials, prefabs, DoorInteraction,
DoorHandleIK, Core/UI assemblies, Input Actions, Packages, ProjectSettings and
SampleScene. No existing metadata is regenerated.

Scene GUID stays `3a4907b3041a2cc4f9b5a0ba4d8976e9`.
New script GUIDs:

- LabRestoreSystem: `a5733b7233d44c198c4609453ad1a3a1`.
- LabInteractable: `fbb578c6d3e948a0b36482f52a79ea67`.

Static verification finds no duplicate GUIDs or unresolved local Scene fileIDs.
The delivered Scene, UI and two new runtime scripts/metadata match the tested
verification copy byte-for-byte.
Final `git diff --check` passes. All 11 stage-11 files decode as UTF-8 and contain
no trailing whitespace. No existing metadata hash changes; nothing is staged.
The existing V1 LF-to-CRLF notices for InteractionSelection/PlayerController1
working copies are not stage-11 changes and are left untouched.

## Backup, environment and actual validation

Before edits, the main Scene/metadata and UI were backed up under:
`D:/Game3D-01/TempTests/QuestV3_20261112_233352/Original/`.
The first LAB layout is retained as `LAB.first-layout.unity`. V1/V2 backups are
not deleted. Native authoring/testing reuses the existing D-drive copy:
`D:/Game3D-01/QuestV1_Backup/HUS_PlayerDuplicateFix/Project`.
Only reviewed Scene/source/probe files are synchronized. No new Unity project
or large cache is created on C and no Packages/ProjectSettings change is made.
The original Editor is not automated or terminated.

Unity version: **6000.6.5f1**.

| Validation | Actual result | Evidence under the stage-11 test directory |
| --- | --- | --- |
| Full production A101 -> A102 -> LAB route, UI and control regression | **282/282 PASS**, 0 failed/runtime errors | `FinalVerified/quest-v3-route.json`, PID 31296, exit 0, no timeout |
| Camera collision regression on the final LAB Scene | **32/32 PASS**, 0 failed/runtime errors | `camera-regression.json`, PID 31696, exit 0, no timeout |
| C# compilation | PASS in native verification copy; no new C# errors/warnings | Final run logs; existing isJumping warning remains |
| GUIDs, metadata and local Scene references | PASS | Final static scope/hash verification |
| Original Game View visual acceptance, mouse buttons, FPS, all hand/camera angles | **NOT TESTED** | Batch screenshot requests produced no PNGs; state/input/color checks are not rendered acceptance |
| NUnit suites | **NOT TESTED** | Runs are standalone native Play Mode probes |

All reports have sibling `.log` and `-process.json` files under
`D:/Game3D-01/TempTests/QuestV3_20261112_233352/`. Parent timeouts can terminate
only the child Unity process started for that run; a timeout is never a pass.

The route uses virtual keyboard state through the real Input System, production
PlayerController, Inventory, doors, IK, quest components and UI. It physically
walks between LAB devices rather than directly setting state or choosing an
abstract sequence in a panel. Checks cover:

- All accepted A101/A102 gameplay, correct keys, pickups, modal isolation and
  room door initial hand contact/follow/release.
- LAB's five targets and selection exclusivity, initial missing-power status,
  breaker-before-inspection rejection, devices-before-power rejection and actual
  power restoration/color changes.
- Returning to the computer for instructions, rejecting B first, connecting A
  then rejecting C and resetting all connection indicators without losing power.
- Walking the retry A -> B -> C, green readiness, no data access before readiness,
  actual data access afterward, Esc dismissal and explicit completion confirmation.
- All 17 journal entries completed, idempotent breaker/devices/completion and
  correct final computer feedback on later interaction.
- J/Tab/E/F/Esc/WASD/Shift/Space/Ctrl isolation and restoration, inventory retaining
  all three keys, existing camera samples and the separate 32-case collision probe.

Failed early reports remain for review. The first revealed a new LAB-only
MaterialPropertyBlock constructor/serialization error, fixed by initializing it
in Awake. A test route also attempted to cross the existing open door leaf and
was routed around it. The next run exposed a genuine narrow aisle caused by the
new devices, fixed by moving only B/C east. No accepted gameplay was changed to
hide failures.

Existing importer messages for `HUSEscape.Player.asmdef.meta` and
`PlayerController.cs.meta` remain: Unity already reports those old files as
malformed/ignored. They are not regenerated in this stage. The existing
PlayerController1.isJumping CS0414 warning and orphan Prefabs 1 metadata remain
untouched. No package/Core/UI workaround is used to suppress them.

## Main Editor acceptance and rerun

Preserve independent unsaved work, stop Play, wait for import and reopen
Floor01_Prototype from disk. Do not run the one-time setup tool again; all new
objects are already saved. Complete A101 and A102 as previously accepted, use
Key_Lab to enter LAB, and follow the updated HUD:

- Central teacher computer near the front writing board: E to inspect.
- West-wall breaker near the room front: E to power on.
- Return to the computer: E for instructions.
- A near the west/rear, B near the east/central aisle, C near the east/rear: E
  in order A-B-C. Try a wrong order first and confirm retry feedback.
- Return to the computer: E for data, then Enter or the completion button.

Esc closes status dialogs; J/Tab retain their prior behavior when no modal is
open. The final data guidance does not load a new scene or open another area.

To rerun the delivered native probe after closing only the verification Editor:

```powershell
powershell -File tools/QuestV3/Run-QuestV3.ps1
```

The runner refuses the main project path, installs only its two probe sources,
backs up their prior versions and writes reports/temp files on D. It does not
delete backups or overwrite gameplay automatically; synchronize only reviewed
production changes if code changes later. Quest state remains session-local,
without save/load, multiplayer, real electrical/network simulation or a new UI
framework. Device arrangement, label readability/colors and UI layout require
manual visual acceptance; actual FPS has not been profiled.
