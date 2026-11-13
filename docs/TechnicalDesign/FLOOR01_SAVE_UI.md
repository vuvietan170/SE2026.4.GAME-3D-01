# Floor 01 polish and Save/Load V1 — Stage 12

This change extends the accepted A101/A102/LAB content in
`Assets/_Project/Scenes/Floor01_Prototype.unity`. It does not introduce another
Inventory, QuestManager, input framework or camera system. All 17 serialized
quest definitions and all existing geometry, transforms, materials, puzzle
rules, door tuning and IK targets remain unchanged.

## State ownership inspected before implementation

| Existing owner | State retained by the checkpoint |
| --- | --- |
| QuestManager | Known gameplay facts; the existing ordered definitions reconstruct current index and NotStarted/InProgress/Completed states |
| PlayerInventory | Ordered IDs of the three ordinary, nonconsumable keys |
| DoorInteraction, three instances | Required-key identity, locked flag and final open/closed pose |
| PickupItem, three instances | Collected/available state, derived from the same Inventory and puzzle flags |
| CodeLockCabinet | A101 solved flag and settled cabinet pose; original Key_A102 visibility |
| SequenceLockPanel | A102 solved flag and settled shutter pose; original Key_Lab visibility |
| LabRestoreSystem | Power, instructions read, count of correctly connected A/B/C devices, data access and floor-completed flag |
| Existing CharacterController/Player transform | Safe grounded position and yaw; spawn fallback if the loaded position is obstructed |

Partial LAB connections are a prefix of the accepted A-B-C order; a count is
sufficient. A102 clue facts may arrive out of order and are retained individually.
No animation state, held input, UI modal, IK coroutine or transient error message
is serialized.

## Persistence and initialization

`HUSEscape.Persistence.Floor01SaveSystem` is a scene-local component on the
existing `Floor01_QuestV1` owner. Its serialized references bind the existing
manager, Inventory, CharacterController, three doors, three original pickups,
two puzzle controllers and LAB system. QuestPuzzleUI explicitly references it.
There is no Singleton or global service locator.

The existing QuestManager initializes at -9500; UI at -9000; Save service captures
the original Player spawn at -8000. Gameplay operations are available after
normal initialization and only when the Player is grounded and door rotation,
pickup, cabinet, shutter and IK operations have settled. The menu explains how
to close it and wait if a paused interaction has not finished.

The default file is `Application.persistentDataPath/floor01-v1.json`. On this
machine Unity reports:

`C:/Users/ASUS/AppData/LocalLow/DefaultCompany/HUS_Escape/floor01-v1.json`

Only this small runtime save belongs there; no save is written under Assets or
the repository. Validation harnesses use an instance-only directory override on
D: and never overwrite the user's real save.

Format version 1 contains scene ID, the exact ordered quest ID signature, facts,
Inventory, keyed door records, puzzle flags, LAB flags, Player pose and UTC save
time. Unknown version/scene/quest layout, duplicate or unknown items/facts,
inconsistent key/reward/door/LAB states, oversized files, invalid UTF-8/JSON or
nonfinite/out-of-bounds positions are rejected before mutating the scene.
Missing files give a normal status message and leave the session unchanged.

Writing uses a UTF-8 `.pending` file and a same-directory move or atomic replace.
Confirmed overwrite retains the previous file as `.previous`; interrupted
pending files are not treated as valid saves. There is no cloud save or automatic
recovery from `.previous` in V1.

Restore applies silent state hooks, disables the CharacterController while
moving, sets doors/cabinet/shutter directly to settled poses and refreshes LAB
indicators. Physics checks require an unobstructed capsule with supporting
ground. An obstructed loaded position uses the original spawn; failure to find
either position rolls back to the previous session. Inventory ownership always
wins over pickup visibility: a collected key cannot respawn or be granted twice.
Restoration never calls normal pickup, puzzle completion or door animation
paths. Quest facts reconstruct all 17 states without emitting progress events.
UI clears stale dialogs and toasts after restore.

## UI and controls

The existing OnGUI implementation remains in place. Layout uses a 1280x720
reference rectangle, scales uniformly for 1920x1080, and centers for other aspect
ratios. Dark opaque panels, light text, 19–26 point reference fonts and consistent
spacing improve contrast. Modal content scrolls inside a bounded body; title and
footer buttons remain outside the scroll area. HUD is hidden while a modal is
open. No Canvas/UI Toolkit migration is involved.

- J opens/closes the existing journal; Esc closes dialogs.
- F5 or **Tiến trình / F5** opens the progress menu using GameplayInputGate.
- **Lưu tiến trình hiện tại** writes the first save. Existing saves require a
  separate **Xác nhận ghi đè** action.
- **Continue** loads without writing the save file.
- **New Game** requires confirmation, resets only the current session, and keeps
  the on-disk save intact. Continue can still restore it afterward.
- Starting Play opens the scene's fresh session; loading is explicit, not automatic.

Existing E/F, J, Tab, movement and puzzle controls continue through their existing
gate. New objective/completion/puzzle/floor notices use QuestManager's event for
new facts only, have a bounded queue and appear outside modal buttons. Repeated
facts do not produce repeated notices. Continue does not replay them.

## Safe warning corrections

Two old metadata import failures were caused by files ending at the last YAML
colon without a final newline. Exactly one LF byte was appended to each:

- `Scripts/Player/HUSEscape.Player.asmdef.meta`, GUID
  `132c76f207af4d8e980833d23edfecde`.
- `Scripts/Player/PlayerController.cs.meta`, GUID
  `a956339eb19f40cbbaa1f1396567a2bd`.

No GUID or YAML value changed. The active PlayerController1 GUID remains
`ca679e99278289644b7f404dea81c8fb`. Native validation confirms the same scene
component now compiles in HUSEscape.Player after the asmdef imports correctly.
The obsolete `isJumping` field had no reads: only that field and its dead
assignments were removed. Animator parameters, controller, clips and timing were
not edited.

## Files in this stage

Modified relative to the start of Stage 12 (earlier uncommitted Quest work is
preserved):

- `Assets/_Project/Scenes/Floor01_Prototype.unity`
- `Assets/_Project/Scripts/Interaction/DoorInteraction.cs`
- `Assets/_Project/Scripts/Interaction/PickupItem.cs`
- `Assets/_Project/Scripts/Inventory/PlayerInventory.cs`
- `Assets/_Project/Scripts/Player/HUSEscape.Player.asmdef.meta`
- `Assets/_Project/Scripts/Player/PlayerController.cs.meta`
- `Assets/_Project/Scripts/Player/PlayerController1.cs`
- `Assets/_Project/Scripts/Puzzle/CodeLockCabinet.cs`
- `Assets/_Project/Scripts/Puzzle/SequenceLockPanel.cs`
- `Assets/_Project/Scripts/Puzzle/LabRestoreSystem.cs`
- `Assets/_Project/Scripts/Puzzle/QuestManager.cs`
- `Assets/_Project/Scripts/Puzzle/QuestPuzzleUI.cs`

Created:

- `Assets/_Project/Scripts/Utilities/Floor01SaveSystem.cs` and `.meta`
  (GUID `b79800a30cf244e8b3f3cd10042b5924`).
- This document.
- Repository-only validation tools in `tools/Floor01Save/`:
  `FloorSaveSetup.cs`, `FloorSavePlayProbe.cs`, `FloorSavePlayValidation.cs`,
  `FloorColdProbe.cs`, `FloorColdValidation.cs`, `FloorGuiReadback.cs`,
  `FloorGuiValidation.cs`, `CameraCollisionProbe.cs`,
  `CameraCollisionValidation.cs`, `Run-FloorSave.ps1`.

The tools are not production Assets and are not attached to the saved Scene.
The Scene delta adds one real save component, its owner/UI references and six
Unity-authored stripped component references for the existing prefab instances.
No existing scene object is removed or moved. No Package, ProjectSettings,
Core, camera, IK, prefab, material, model, Avatar or animation asset is modified.

## Verification and limitations

Final reports and images are under
`D:/Game3D-01/TempTests/Floor01Save_20261113_001225/`.
`Original/` retains the pre-change Scene and relevant files. Previous Quest
backups remain intact.

The old reusable verification copy lacked this project's URP settings/assets;
its early render/FPS results are not representative and are superseded. Final
validation uses `ExactProject/`, a new D: copy of the main project's current
Assets/Packages/ProjectSettings, with only temporary test harnesses added.
Unity 6000.6.5f1 runs in a separate process with a 420-second parent timeout.
No existing Editor process is stopped. The harness redirects TEMP and GI cache
to D:; Unity's initial import created its default GI cache directory before that
explicit override was added. No cache cleanup or global preference change was
performed.

Native runtime validation drives Input System keys through the real production
components. It uses a fixed simulation timestep for deterministic movement,
without replacing Inventory/puzzles/doors/save storage with fakes. Checkpoint
tests call the same public save APIs used by the OnGUI buttons. They are native
Play Mode smoke tests, not NUnit Edit Mode/Play Mode test-runner results.

Eight saved checkpoints cover: opened A101; solved A101 with uncollected reward;
partially read A102 clues; solved A102 with uncollected reward; collected Key_Lab;
restored LAB power; LAB with only A connected; completed floor. Every checkpoint
goes through New Game and Continue, comparing facts, items, door poses, reward
visibility, LAB state, safe Player pose and unchanged save bytes.

Additional checks cover missing/corrupt/incompatible saves, mismatched quest
signature, duplicate items, overwrite refusal, obstructed-position fallback,
repeated loading, duplicate rewards/notices, full A101/A102/LAB progression,
input gating and the existing shared-hand IK release.

The first two full runs in ExactProject retained FAIL results because an Editor
startup exception was counted as a gameplay error. The stack is confined to
`UnityEditor.Search.SearchDatabase` / `IndexationOnStartup`. Later reports retain
it explicitly in `editorStartupErrors`; runtime errors remain separate. No
Search code/package/configuration was modified or error output suppressed.
This is a remaining Editor integration issue, not a claim of an error-free
Unity Console. Licensing refresh/Curl messages and cold-import package shader
dependency diagnostics also remain available in the raw logs.

Final results (all Unity processes below exited 0 without timeout):

| Verification | Result | Evidence folder | Process |
| --- | --- | --- | --- |
| Full route, eight checkpoints, invalid files and regressions | PASS 388/388; zero production runtime errors; one separately recorded Editor startup error | FinalGameplay | PID 27216 |
| Cold Continue in a fresh Editor process | PASS 10/10; zero production runtime errors; one Editor startup error | FinalCold3 | PID 5968 |
| Existing camera collision regression | PASS 32/32; 3,178 safety frames; zero penetration and stationary jitter; one Editor startup error | FinalCamera | PID 25272 |
| Actual Game View UI capture | PASS 14 images, seven states at each resolution | FinalVisual2 | PID 32624 |
| Unity compilation | PASS in the D: copy, no C# errors/warnings or the two old metadata import errors in final runs | Editor logs in these folders | Unity 6000.6.5f1 |
| Original open Editor manual route/mouse acceptance | NOT TESTED by this task | User acceptance steps below | Not driven |

Each evidence folder contains `floor-save-route.json`, `floor-save-route.log`
and `floor-save-route-process.json`. The cold-load harness initially called
Continue before gravity settled the Player; the service safely refused. That
FAIL is retained under `FinalCold/`. `FinalCold2/` waits for grounded state and
passes without changing production logic; it exposed an obsolete API warning
in the test harness. `FinalCold3/` uses FindAnyObjectByType instead and passes
with no C# warnings. Earlier unsettled/render-texture
captures are superseded by the end-of-frame captures under `FinalVisual2/`.

The final idle Editor sample at 1920x1080 measured **104.1 FPS** (521 frames /
5.005 seconds), RTX 3050 Ti Laptop GPU, VSync 0, targetFrameRate -1. No performance
settings or production frame cap were changed. It is not a moving full-route
benchmark or standalone build result.

Final static checks found 159 asset metadata GUIDs with no duplicates, 3,602
unique Scene document IDs and all 23 distinct script GUIDs resolved through
Assets or installed packages. All main-project production Assets matched the
tested copy byte-for-byte. Against the Stage 12 baseline there are 12 modified
files and 13 new files (including this document and ten tools), with no deletions.
The 17-definition QuestManager Scene block is byte-identical; only the owner/UI
blocks and seven additional reference/service blocks differ. Packages,
ProjectSettings, SampleScene, prior Quest tools/docs and all other assets remain
unchanged. `git diff --check` passes; Git still prints pre-existing line-ending
conversion notices. Nothing is staged, committed or pushed.

Visual captures exercise seven real
OnGUI states at each of 1280x720 and 1920x1080, using native Game View end-of-frame
capture. Long journal text scrolls without overlapping footer buttons. The FPS
sample is a short idle Editor/UI sample on this machine, not a standalone build
benchmark or a minimum FPS guarantee. Manual mouse-button acceptance and the
full route in the user's original open Editor are still required; automated
results must not be described as that manual acceptance.

V1 is deliberately specific to this floor's 17 definitions, three existing keys
and two puzzles. Changes to those identifiers require a future format migration.
There is one manual slot, no autosave and no persistence during unfinished
animations. Door restoration snaps to a saved final state by design.

## Editor acceptance

1. Open Floor01_Prototype in the main project and enter Play; check the initial
   HUD and J journal at both target resolutions.
2. Open F5. Choose New Game and confirm. Follow A101 (2018), A102 (A-C-B) and LAB
   (power, instructions, physical A-B-C, data and final confirmation).
3. At each of the eight checkpoints above, stand still on open ground, let all
   animations settle, use F5 to Save, and confirm overwrite only when intended.
4. New Game, then Continue; verify Inventory, HUD, doors, cabinet/shutter and LAB
   indicators match. Stop/restart Play and Continue again.
5. Check the mouse buttons, scroll wheel and Esc/J/Tab/E/F/movement suppression
   while a modal is open; inspect camera and hand contact visually.
6. Keep the save before experimenting with corrupt data. Never edit save files
   under Assets or change asset GUIDs.
