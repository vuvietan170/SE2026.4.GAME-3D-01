# Quest V2 — A102 chronological order puzzle

Implemented on the existing `feature/fo-005-quest-v1` branch, 2026-11-12.
Uncommitted Quest V1 work was preserved; no branch switch, staging, commit,
push, pull request, merge, reset or deletion was performed.

## Player flow

Complete A101 as before: Key_A101 -> A101 -> clue -> code 2018 -> Key_A102.
Unlock A102 with that key and enter it. The existing room trigger advances the
same QuestManager to A102 objectives. Read the three notes using F:

| Event | Sample year | Location |
| --- | --- | --- |
| A | 2016 | Posted near the writing board, at (17, 2.1, 28.24). |
| B | 2020 | On the student desk at (15, 0.835, 31). |
| C | 2018 | On the student desk at (18.8, 0.86, 39). |

At the former Key_Lab desk, press E to open the sequence panel. Select the events
from earliest to latest: **A -> C -> B**, then Enter or Confirm. Keyboard A/B/C,
1/2/3, numpad 1/2/3 and the three mouse buttons share the selection path.
Backspace removes the last selection; Delete/the Clear button clears it.
Duplicate selections, missing/invalid indices and incorrect chronological order
are rejected with feedback. Esc/Close dismisses the panel.

All three clues and the A102 room-entry fact are required even if the correct
order is guessed early. Once solved, the reward shutter rises. Only after the
opening animation finishes is the original Key_Lab pickup activated. Press F
to pick it up using the existing Inventory, then unlock and enter LAB.

J retains the journal. Tab retains the 12-slot Inventory. Sequence UI uses the
same GameplayInputGate as V1; gameplay input cannot leak behind the modal.
No puzzle is added to LAB.

## Minimal architecture extension

- `QuestManager.HasObserved(eventId)` exposes a read-only fact query. Existing
  ordered progression, early-fact handling and Inventory observation are reused.
  No second manager, inventory or global event system is introduced.
- `ClueNote` gains a configurable `Title`, defaulting to the existing A101
  caption. Existing content/event behavior is unchanged; A102 uses three copies
  of the same component with separate titles and event IDs.
- `SequenceLockPanel` is the only new runtime component. It stores three event
  names/years and the required clue facts, validates a unique increasing-year
  permutation and opens its explicitly bound shutter. Its defaults are A=2016,
  B=2020, C=2018; changing distinct serialized years changes the accepted order.
  The UI does not reveal these years; they are discovered on the clue notes.
- `QuestPuzzleUI` gains a Sequence mode and nearest-panel binding, reusing its
  existing journal, clue dialog, input gate, error message area and Close/Esc
  behavior. Existing enum values and the A101 PIN path are retained.

The same manager's serialized list grows from 8 to 12 objectives. Four entries
are inserted after entering A102 and before picking up Key_Lab:

1. Read clue A (`clue:A102:A`).
2. Read clue B (`clue:A102:B`).
3. Read clue C (`clue:A102:C`).
4. Solve the sequence (`sequence:A102`).

Facts can be read in any order; the existing V1 fact mechanism retains early
reads. The current HUD step still progresses in the configured order, while the
panel shows the actual number of clues read. Picking up Key_Lab advances to the
existing LAB objective on the next Inventory-observation Update.

The panel starts the reward inactive in the saved Scene and enforces this in
Awake. Incomplete panel references fail closed with an error rather than exposing
the key. A solved panel cannot grant the reward a second time. Its shutter uses
unscaled time, matching V1's modal pause model, and adds no Inventory item directly.

## Saved Scene scope and safety

Scene: `HUS_Escape/Assets/_Project/Scenes/Floor01_Prototype.unity`.
New objects are grouped under `Floor01_QuestV2_A102`:

- Three clue notes and their local labels.
- A102_SequencePanel with SequenceLockPanel.
- A small reward compartment around the existing Key_Lab desk, using five solid
  box parts (back, two sides, top, sliding shutter) and existing materials.
- A panel label and a hidden reward-label group.

Key_Lab's original PickupItem, Item Id, prefab GUID, transform and inventory path
are retained. Only its scene instance's initial active state changes; its existing
labels are grouped so they follow reward visibility. The shutter rises vertically
instead of swinging into the Player or a return aisle. No existing furniture,
room dimensions, room doors, hinges, HandleTargets, Player spawn or camera tuning
is changed. No new materials, textures, packages or ProjectSettings are required.

Static comparison against the pre-stage-10 snapshot finds eight existing Scene
blocks changed and 76 new blocks, with no deleted block. The only modified
existing MonoBehaviour block is the QuestManager definition list. Extra default
serialization from Unity for the camera/A101 was excluded from delivery; original
camera, A101 clue and A101 cabinet serialized blocks are kept verbatim apart from
normal line endings/trailing-space normalization. No test probe is saved in Scene.

## Files changed in stage 10

Modified (4):

- `HUS_Escape/Assets/_Project/Scenes/Floor01_Prototype.unity`
- `HUS_Escape/Assets/_Project/Scripts/Puzzle/QuestManager.cs`
- `HUS_Escape/Assets/_Project/Scripts/Puzzle/ClueNote.cs`
- `HUS_Escape/Assets/_Project/Scripts/Puzzle/QuestPuzzleUI.cs`

New (7):

- `HUS_Escape/Assets/_Project/Scripts/Puzzle/SequenceLockPanel.cs`
- `HUS_Escape/Assets/_Project/Scripts/Puzzle/SequenceLockPanel.cs.meta`
- `tools/QuestV2/QuestV2Setup.cs`
- `tools/QuestV2/QuestV2PlayProbe.cs`
- `tools/QuestV2/QuestV2PlayValidation.cs`
- `tools/QuestV2/Run-QuestV2.ps1`
- `docs/TechnicalDesign/QUEST_V2_A102.md` (this document).

No existing file is deleted. The original uncommitted V1 source, metadata,
documentation and tools remain present. PlayerController1, GameplayInputGate,
Inventory/UI, InteractionSelection, CodeLockCabinet, QuestRoomTrigger,
DoorInteraction, DoorHandleIK, camera, Core/UI assemblies, Input Actions,
model/Avatar/Animator/eight Mixamo clips, materials, prefabs, SampleScene,
Packages and ProjectSettings retain their pre-stage-10 hashes.

The scene GUID remains `3a4907b3041a2cc4f9b5a0ba4d8976e9`.
The new SequenceLockPanel GUID is `6e0d0eae132f4665afb603825254dfb6`.
No pre-existing metadata is regenerated. Static checks find no duplicate asset
GUIDs and no unresolved local Scene fileIDs. Runtime setup resolves the existing
QuestManager, inventory, UI and pickup references. Vietnamese text remains UTF-8.
Final `git diff --check` passes. All 11 stage-10 files decode as UTF-8 and have
no trailing whitespace. Git only emits the existing LF-to-CRLF notices for the
V1 InteractionSelection/PlayerController1 working copies; those files were not
changed in stage 10. Nothing is staged.

## Backup and validation evidence

Pre-change Scene, metadata and affected V1 scripts are backed up at:
`D:/Game3D-01/TempTests/QuestV2_20261112_223943/Original/`.
The first native authored Scene is also retained as `Scene.initial-authoring.unity`.
No backup was deleted. Temporary output is on D, with no new Unity project or
Library on C.

Native validation reused the existing D-drive verification project:
`D:/Game3D-01/QuestV1_Backup/HUS_PlayerDuplicateFix/Project`.
Only reviewed source/Scene files and validation scripts were synchronized; the
project's Assets/Library/Packages/ProjectSettings were not copied wholesale.
The delivered Scene and four involved runtime source files match the final
tested copy byte-for-byte.

Unity version: **6000.6.5f1**.

| Executed validation | Result | Evidence |
| --- | --- | --- |
| Full V2 production route and V1/control regression | **186/186 PASS**, 0 failed, empty runtime-error list | `Final/quest-v2-route.json`, PID 27052, exit 0, no timeout |
| Existing camera collision regression on V2 Scene | **32/32 PASS**, 0 failed, empty runtime-error list | `camera-regression.json`, PID 16952, exit 0, no timeout |
| C# compilation | PASS in verification project; no new C# errors | Native setup/final run logs |
| Manual rendered UI, mouse keypad clicks, all camera/hand angles, FPS | **NOT TESTED** | Batch screen capture produced no PNG; state/input checks do not establish visual quality |
| NUnit suites | **NOT TESTED** | These are standalone native Play Mode probes |

All evidence is under `D:/Game3D-01/TempTests/QuestV2_20261112_223943/` with sibling
`.log` and `-process.json` files. Main Editor sessions were not driven or killed.
Parent timeouts only target the child process started for that run.

The final native route verifies:

- The original A101 key/clue/PIN/cabinet and three ordinary door IK paths.
- Earned Key_A102 is retained when A102 unlocks; entering A102 advances the
  original manager/HUD state. Initially locked room doors reject E without keys.
- F cannot pick up Key_Lab while locked. Even correct A-C-B before reading the
  clues is rejected. Clues C, then A, then B are read through real F input.
- J shows all three A102 clue goals complete and the sequence goal in progress.
- Sequence modal blocks WASD/E/F/Tab/J/Space/Ctrl gameplay leakage. Wrong A-B-C,
  duplicate selections, malformed permutations and missing entries fail safely.
- Delete, Backspace and numeric 1-3-2 select and submit A-C-B through the actual UI.
- Reward remains inactive during opening; the completed shutter enables the same
  Key_Lab, without pushing the Player. Actual F pickup updates existing Inventory.
- Actual movement returns to the corridor, opens LAB and completes all 12 quests.
  Inventory retains all three keys. Classroom aisles, room portals, camera
  visibility samples, initial hand contact/follow/release, walk/sprint/jump/crouch,
  J/Tab/Esc and modal teardown remain functional.

The first V2 route attempt reached successful Key_Lab pickup but asserted the HUD
step before the existing QuestManager Inventory adapter's next Update. The test
was corrected to wait for that existing observation cycle; production inventory
or QuestManager update behavior was not replaced. That failed report is retained.

The existing camera probe passed 3,178 monitored frames with zero measured
penetration. Original old import messages for PlayerController.cs.meta and
HUSEscape.Player.asmdef.meta remain untouched; those files already failed import
before this task. The existing isJumping CS0414 warning appeared in native setup.
No package/Core/UI changes were used to hide those unrelated issues.

## Acceptance in the main Editor

Preserve independent unsaved work, stop Play, allow script import and reopen
Floor01_Prototype from disk. Do not run either one-time setup tool against the
already configured Scene. V2 objects are already saved and editable.

Play A101 exactly as before. In A102, read the note near the front writing board
and the two notes on the student desks. Use J to inspect progress, then E at the
former Key_Lab desk. Try A-B-C first, clear it, select A-C-B and confirm. Wait for
the shutter to finish before F pickup. Continue to LAB with Key_Lab.

To rerun the standalone route, close only the verification copy's Editor and run:

```powershell
powershell -File tools/QuestV2/Run-QuestV2.ps1
```

The runner installs only its two test sources into the existing copy and backs
up previous probe versions. It refuses the main project path, keeps outputs and
temporary files on D, and never deletes a backup. Synchronize only reviewed
production changes if they change later; the runner deliberately does not
overwrite gameplay or Scene files automatically.

Quest state remains session-local as in V1; there is no save/load or branching
framework. Years/clues are sample content. The five new compartment colliders
and clue labels need manual visual acceptance at varied resolutions and camera
angles; automated waypoint/physics checks are not an FPS or visual-quality claim.
