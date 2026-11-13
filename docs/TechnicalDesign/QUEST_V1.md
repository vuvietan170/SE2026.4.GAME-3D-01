# Quest V1 recovery and A101 code puzzle

Date: 2026-11-12. Branch: `feature/fo-005-quest-v1`.

## Recovered implementation

Quest V1 was still confined to the D-drive backup when work resumed. The main
project contained none of the six new runtime components. Comparison of the
three existing scripts found only the planned modal-input guards. Their metadata
matched semantically (line endings differed), so existing `.meta` files were
left untouched. No newer gameplay logic was overwritten.

Recovery source, reused in place for native validation:
`D:/Game3D-01/QuestV1_Backup/HUS_PlayerDuplicateFix/Project`.
The deleted C-drive verification project was neither searched for nor recreated.

The main scene and three existing scripts were backed up before editing at:
`D:/Game3D-01/TempTests/QuestV1_Resume_20261112_220816/Original/`.
The pre-recovery Quest scene is retained as `QuestScene.before.unity` beside that
folder. No backup or user data was deleted. Native logs/output/temp files remain
on D; no extra Unity project or Library was created on C.

Only the eight Quest-related modifications and 73 added serialized blocks were
merged into the main scene. Original objects remain present. The extra camera
serialization in the backup was excluded, keeping the main camera configuration
unchanged. The scene and its original metadata/GUID remain authoritative.

## Components and progression

- `QuestManager` holds ordered quest definitions and NotStarted / InProgress /
  Completed states. `Signal(eventId)` records configured gameplay facts once,
  including facts received before their step becomes current. There is no global
  event bus, Singleton, duplicate inventory or dependency on Core implementation.
- `QuestRoomTrigger` emits room-entry facts only for the assigned PlayerInventory.
  Its colliders are triggers and are ignored by the existing camera queries.
- `ClueNote` provides the readable A101 clue and signals `clue:A101`.
- `CodeLockCabinet` validates exactly four ASCII digits. Only `2018` unlocks this
  sample cabinet. It opens its own leaf and enables the existing Key_A102 pickup
  only after the leaf finishes opening. No inventory item is granted directly.
- `QuestPuzzleUI` uses the existing IMGUI approach for HUD, J journal, F clue and
  E keypad. Keyboard digits/numpad, Backspace, Delete, Enter, Esc and mouse keypad
  buttons share the same production methods. Wrong input leaves the dialog open
  with feedback. Journal, clue and keypad have close buttons.
- `GameplayInputGate` is local to the Player. While a modal is open it suspends
  the enabled Player input actions, pauses gameplay and temporarily disables the
  existing camera follow component without editing it. Cursor state, action
  state and time scale are restored. Delayed release prevents closing-frame
  E/F leaking into gameplay and avoids resuming SmoothDamp at deltaTime zero.
- Existing PlayerController1.Update, InteractionSelection.Resolve and InventoryUI
  have small gate checks. InventoryUI exposes its existing open state so the
  journal cannot overlap Tab inventory. Inventory storage and capacity stay intact.

The eight initial objectives are:

1. Pick up Key_A101.
2. Unlock A101 and enter the room.
3. Read the clue on the teacher desk with F.
4. Unlock the A101 cabinet with code 2018.
5. Pick up Key_A102 from the open cabinet with F.
6. Unlock A102 and enter it.
7. Pick up Key_Lab.
8. Unlock LAB and enter it.

Inventory has no notification API, so QuestManager observes its existing HasItem
facts as a small integration adapter; it does not copy or replace stored items.
Room/clue/cabinet facts are emitted by their production components.

## Saved Scene

`Assets/_Project/Scenes/Floor01_Prototype.unity` contains a `Floor01_QuestV1`
owner with explicitly bound manager/UI, the clue on the A101 teacher desk,
the cabinet around the existing Key_A102 location, and three room-entry triggers.
GameplayInputGate is attached to the existing Player and references the same
Input Actions asset and camera-follow component.

Key_A102 keeps its original pickup, Item Id, GUID, transform and Inventory path.
Its scene prefab instance starts inactive. The cabinet also enforces inactivity
in Awake; the key cannot be selected before the cabinet animation completes.
The key's labels are grouped with the reward and hidden while locked. The cabinet
closes the front of the old key desk and opens inward to avoid pushing the Player
or blocking the return aisle. All existing architecture, furniture, Player spawn,
door hinges, required keys and HandleTargets are preserved.

No puzzle was added to A102 or LAB; their triggers only report room entry.
SampleScene is untouched. Model, Avatar, Animator Controller, eight Mixamo clips,
camera code, DoorInteraction, DoorHandleIK, PlayerInventory, Input Actions,
Packages, ProjectSettings and Core/UI implementation files are unchanged.

## Exact files in the main repository

Modified (4):

- `HUS_Escape/Assets/_Project/Scenes/Floor01_Prototype.unity`
- `HUS_Escape/Assets/_Project/Scripts/Interaction/InteractionSelection.cs`
- `HUS_Escape/Assets/_Project/Scripts/Inventory/InventoryUI.cs`
- `HUS_Escape/Assets/_Project/Scripts/Player/PlayerController1.cs`

New runtime source files, each with its recovered original `.meta` (12 files):

- `HUS_Escape/Assets/_Project/Scripts/Player/GameplayInputGate.cs`
- `HUS_Escape/Assets/_Project/Scripts/Puzzle/ClueNote.cs`
- `HUS_Escape/Assets/_Project/Scripts/Puzzle/CodeLockCabinet.cs`
- `HUS_Escape/Assets/_Project/Scripts/Puzzle/QuestManager.cs`
- `HUS_Escape/Assets/_Project/Scripts/Puzzle/QuestPuzzleUI.cs`
- `HUS_Escape/Assets/_Project/Scripts/Puzzle/QuestRoomTrigger.cs`

New reviewable validation sources/documentation (4):

- `tools/QuestV1/QuestV1PlayProbe.cs`
- `tools/QuestV1/QuestV1PlayValidation.cs`
- `tools/QuestV1/Run-QuestV1.ps1`
- `docs/TechnicalDesign/QUEST_V1.md`

Total: 4 modified, 16 new, 0 deleted files. The unrelated untracked
`Assets/_Project/Prefabs 1.meta` already existed before this task and is retained.
QuestV1Setup remains a disposable authoring harness in the backup; it is not
installed into the main project's Assets. Existing folder structure is reused.

## Actual native results and evidence

Unity 6000.6.5f1 runs used the existing D-drive verification copy. The main Scene
and all nine involved runtime source files match the tested copy byte-for-byte.
The original open Editor was not automated; no result below claims otherwise.

| Check | Status | Evidence |
| --- | --- | --- |
| Quest initialization, three states and ordered/out-of-order facts | PASS | Final native route, 152/152 checks |
| Key_A101 pickup, normal A101 opening and initial hand contact/release | PASS | Real virtual keyboard F/E and production components |
| F clue, J journal, Esc close and modal input isolation | PASS | Native route includes simultaneous gameplay keys, crouch preservation and teardown |
| E keypad, wrong/malformed PIN rejection, 2018 success | PASS | Production UI entered through E and keyboard digits/Enter |
| Reward inaccessible before/during cabinet opening | PASS | Actual F attempt before solving; inactive key while leaf opens |
| Cabinet does not push the Player; F pickup uses existing inventory | PASS | Position assertion and actual Key_A102 pickup |
| A102, Key_Lab, LAB and all eight quest completions | PASS | Actual WASD route, unique F/E targets, three keys retained |
| WASD, Shift, Space, Ctrl and Tab after dialogs | PASS | Native walk/sprint/jump/landing/crouch/inventory assertions |
| Existing camera collision regression | PASS | 32/32 checks; 3,178 monitored frames, zero measured penetration |
| Compilation/new runtime errors | PASS | Native runs exit 0; no new C# errors or captured runtime errors |
| GUID uniqueness and local Scene references | PASS | Static verification; no duplicates/unresolved local fileIDs |
| Rendered HUD/journal/keypad text and mouse button clicks | NOT TESTED | Batch screenshot requests produced no PNG; keyboard/state checks are not visual acceptance |
| Manual original Editor walkthrough, FPS and every camera/hand angle | NOT TESTED | Requires human Game View acceptance |
| NUnit suites | NOT TESTED | These runs use standalone native Play Mode probes, not NUnit |

Final Quest run:
`D:/Game3D-01/TempTests/QuestV1_Resume_20261112_220816/Final/quest-route.json`
(152 passed, 0 failed, empty runtime-error list, visited A101/A102/LAB).
The sibling `quest-route-process.json` and `quest-route.log` record the actual
PID, exit code 0 and no timeout. The delivered runner was used for this final run.

Camera regression:
`D:/Game3D-01/TempTests/QuestV1_Resume_20261112_220816/camera-regression.json`
(32 passed, 0 failed; PID 20996, exit 0, no timeout), with sibling log/process file.

The earlier `quest-route*.json` attempts are retained as evidence. The original
failed run stopped because Input Actions and package-cache files were missing
from the copy; those specific missing files/cache were restored from the main
project without modifying its Packages or Core/UI. A later harness check counted
new trigger zones as solid door obstructions; only that test was corrected to
ignore triggers. Cabinet tests now wait for real unscaled animation time rather
than assuming 60 simulated frames equal 0.45 seconds of real time.

One production issue was found and fixed: the unfinished cabinet opened outward,
pushing Player to x=9.79, outside pickup range. Only the new cabinet's opening
angle was changed to -90 degrees into its interior. The final test proves no
significant Player displacement, with successful pickup and return route.

## Metadata and existing issues

The main scene GUID remains `3a4907b3041a2cc4f9b5a0ba4d8976e9`.
PlayerController1 GUID remains `ca679e99278289644b7f404dea81c8fb`.
All pre-existing `.meta` hashes remain unchanged. Six recovered component GUIDs
are preserved and resolve to the expected runtime classes. No duplicate GUIDs,
missing local Scene fileIDs or temporary probe objects are saved in the scene.
UTF-8 decoding succeeds and the Vietnamese clue text is intact.

Existing unrelated import messages still appear for:

- `Assets/_Project/Scripts/Player/HUSEscape.Player.asmdef.meta`
- `Assets/_Project/Scripts/Player/PlayerController.cs.meta`

Unity reports these old metadata files as malformed/ignored. They were present
before recovery and are not regenerated or repaired in this task. As in the
existing project, active scene components compile into Assembly-CSharp. Repairing
the asmdef metadata later can change assembly boundaries and needs a separate
dependency review; do not simply regenerate GUIDs. The existing isJumping CS0414
warning and orphan Prefabs 1 folder metadata also remain untouched. No new C#
error is hidden by changing packages, assembly definitions or Core/UI code.

## Playing and rerunning

In the original Editor, preserve any independent unsaved work, stop Play and
allow imports/script reload. Open Floor01_Prototype from disk. Do not run the old
floor builder or the disposable Quest setup again; the finished objects are
already saved in the scene.

Pick up Key_A101 with F, open A101 with E, and read the paper on the teacher desk
with F. Close with Esc. Return to the former Key_A102 desk, press E at the cabinet,
try an incorrect PIN, then enter 2018 and press Enter. Wait for the cabinet to
finish opening, then press F to pick up Key_A102. Continue through A102 to Key_Lab
and LAB. J toggles the journal; Tab retains the original 12-slot inventory. Close
the inventory before opening the journal or a puzzle UI.

To repeat native verification, first close only the verification copy's Editor:

```powershell
powershell -File tools/QuestV1/Run-QuestV1.ps1
```

The runner reuses the D-drive copy, imports only the two validation sources,
saves their previous versions in its output folder and never copies whole project
directories or modifies build settings. It refuses the main project path and
uses a parent timeout that can terminate only its own Unity process. It does not
delete the backup. If production files change later, compare/synchronize those
specific files first; the runner intentionally does not overwrite gameplay.

## V1 limitations

Quest/puzzle state is session-local; no save/load, branching, rewards framework,
localization framework or new inventory is introduced. Restarting Play resets
the puzzle. Code 2018 is only a sample clue. Modal UI pauses scene gameplay;
it is not a multiplayer/time-independent interaction system. Decorative cabinet
geometry is primitive-based. Visual layout at small resolutions, mouse keypad
clicks, all camera angles and animated mesh contact need manual acceptance.
No branch switch, staging, commit, push or merge was performed.
