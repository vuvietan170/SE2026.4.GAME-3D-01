# VT-103 Step 6 - Floor01 gameplay autosave integration

Worktree: `D:/Game3D-01/VT103_Work`; branch `individual/23001853-vt103-autosave`.
Baseline: `f933595ef7d1aa810032ddd54cafaad4a438aea4`. No commit, push, merge, mutation rerun, benchmark rerun or PDF.

## Implementation

- `Floor01SaveSystem.Awake` explicitly binds one scene-local `Floor01Autosave` to itself and the existing QuestManager. No scene edit, persistent owner, Inventory copy or global Unity service is introduced.
- `QuestManager.ProgressRecorded` is filtered to Floor01 definition facts. A 0.3 s unscaled debounce coalesces progress, rather than a periodic ten-second timer. Repeated facts generate no work. Update polls pending work; it does not write every frame.
- Capture runs on Unity's thread using existing `CaptureForTravel` validation: V1 DTO, all 17 quest IDs/facts, inventory, three doors, cabinets, LAB, and safe player pose. Manual menu feedback is preserved. Unsafe capture retains the dirty flag and retries after 0.5 s. A persistence error retains progress, exposes LastError, and retries after 2 s. CommitCount changes only after an acknowledged real commit.
- Immutable JSON crosses the worker boundary. `DraftStore.ReserveRevisionAsync` reserves a revision above validated disk head and all same-process/path reservations, including across instances. Revision gaps on cancellation are permitted. A GUID identifies each captured request. Original stale/idempotency/conflict guards remain active.
- Draft: `Path.Combine(Application.persistentDataPath, "floor01-draft-v1.json")`. Envelope formatVersion 1; payload is unchanged Floor01 save V1 JSON. Corrupt metadata/fingerprint fails closed. No draft payload is loaded into gameplay.
- `OnDisable/OnDestroy` unsubscribe and cancel pending jobs. Static async continuations retain only immutable payload/store/token, not the destroyed component. Faults are observed even after scene destruction. Cancellation is checked before committing and cleans only its own staging file. An atomic replacement already entered cannot be interrupted.
- New Game, Continue and travel restore cancel pending autosave and drain/fence current commits using the same per-path semaphore before applying state. `IsRestoring` suppresses capture. The lease releases in finally, including rollback. No worker continuation needs Unity's context. The synchronous fence can wait on an active filesystem operation; there is no hard I/O timeout.
- The original one-argument SaveAsync API is preserved alongside the cancellable overload. Runtime autosave asmdef remains pure BCL/noEngineReferences; component remains in the predefined Assembly-CSharp beside Floor01SaveSystem. The new Play Mode asmdef references only the pure writer; reflection crosses the predefined-assembly boundary in tests, with no gameplay assembly restructuring.

## Test-first and actual results

Unity: **6000.6.5f1 (3ff58d469c8a)**. Only targeted VT103 suites were run.

| Evidence directory | Executed | Passed | Failed | Exit | Interpretation |
|---|---:|---:|---:|---:|---|
| red-20261113-185509 | 5 | 0 | 5 | 2 | All five assertion failures, no compile/assembly failures; inert integration scaffold archived in source/ |
| green-20261113-185917 | 5 | 0 | 5 | 2 | Initial patch did not reach safe capture; I03 invocation error. Not GREEN |
| diagnose-20261113-190117 | 1 | 0 | 1 | 2 | Runtime diagnostics showed grounded=false, all door/IK/pickup/cabinet flags idle |
| green-clock-20261113-190227 | 5 | 5 | 0 | 0 | Deterministic 60 Hz fixture resolves headless grounding; gameplay source/safety checks unchanged |
| integration-final-20261113-190445 | 12 | 11 | 1 | 2 | I08 incorrectly used wall time for simulated flight duration; not a clean PASS |
| integration-final-20261113-190649 | 12 | 12 | 0 | 0 | I08 checks actual airborne/grounded state, not assumed duration |
| store-final-20261113-190827 | 11 | 10 | 1 | 2 | T03 staging path exceeded Mono/Windows path handling in nested evidence; environment error, not assertion evidence |
| store-final-20261113-191008 | 11 | 11 | 0 | 0 | Short D: fixture path, unchanged T01-T08, all writer checks pass |
| gameplay-green-final-20261113-191142 | 12 | 12 | 0 | 0 | Final production/test source, 16.91 s, no timeout, clean Editor exit |

Final total: **23/23 targeted tests passed, 0 failed, 0 blocked** across two clean Editor processes. Editor compilation succeeded for the selected project scripts/assemblies; no `error CS` in these final logs. XML and process exit/timeout are recorded separately; no shutdown hang was counted as a clean run. Earlier failed experiments remain preserved.

I01-I05 were written and executed before the integration patch. Subsequent fixture fixes and additional tests are recorded separately; do not mistake the final fixture for byte-identical original RED source.

## Requirement mapping

| Test | Independent observable requirement |
|---|---|
| I01 | New Floor01 quest fact creates a real draft; decoded V1 payload contains expected scene, version, inventory and fact |
| I02 | Repeated fact plus idle frames do not produce another commit or modify bytes |
| I03 | Actual component capture and commit methods, submitted in reverse order, reject older revision and preserve newer disk head |
| I04 | All three manual slots remain byte-identical after real autosave |
| I05 | Controlled real filesystem failure (directory occupying draft path) exposes error, no success, active inventory/quest intact |
| I06 | Actual Floor01 scene composes one autosave owner automatically, startup alone does not commit |
| I07 | Explicit New Game cancels debounced work; restored old/fresh state is not autosaved |
| I08 | Actual airborne CharacterController makes capture reject; pending progress commits after actual grounding |
| I09 | Re-enable cycles do not duplicate subscriptions; disabled owner no longer receives progress |
| I10 | Corrupt draft bytes remain intact, active gameplay unaffected |
| I11 | Existing head revision 100 is exceeded, opaque old payload is never auto-loaded |
| I12 | Real manual Save/Continue retains its own semantics; newer draft does not replace manual restoration or get rewritten by restore |
| S01 | Disk head and process reservations monotonically seed revisions across canonical-path aliases without writing |
| S02 | Cancellation after actual staging does not commit, preserves old head, cleans owned staging and permits retry |
| S03 | A restore fence blocks another instance's writer until disposed |
| T01-T08 | Existing revision, stale, idempotency, identity conflict, concurrent access, persistence failure and controlled scheduling checks |

These are Unity **component integration tests**, not input E2E. Real Floor01 is loaded additive; JourneySceneEntry startup is disabled in the fixture to avoid persistent journey/test-runner interference. Tests call the existing production Inventory/Quest APIs, not keyboard pickup animation. Fixed captureDeltaTime=1/60 is restored in teardown; no Player, IK, Camera or Animator parameters were changed. Fixture GUID directories isolate shared state. All fixture/cache locations are D:. Latest fixtures are retained at the short path in environment.json and copied into each evidence run's test-data/.

## Compatibility, review and limits

- Manual slot schemas, paths, overwrite confirmation and Continue/New Game selection are unchanged. No write to floor01-v1.json, campus-v1.json or trainingoffice-v1.json by autosave. I04 and I12 cover isolation and actual manual restoration.
- No draft recovery button/automatic restore exists in this slice. F5 Continue still restores the manual slot. A later explicit recovery feature must validate the complete payload and ask the player before changing progress.
- Scene unloading cancels uncommitted debounce/jobs. There is no flush-on-quit guarantee: exiting before debounce/safe capture can leave the previous draft as latest. A replacement already in progress may finish, but same-path reservation and stale guards prevent overwriting an already committed newer revision. Native scene-transition timing/full-session E2E and visual/input acceptance are **NOT TESTED** in this step.
- Same canonical lexical Windows path, **one process only**. No cross-process lock, distributed ordering, symlink/hardlink identity resolution or crash/power-loss durability proof. Historical request identity persistence across process restart is not extended by this integration.
- No post-integration performance or coverage claim: previous benchmark/mutation evidence remains historical and unchanged. Main Gate, Campus/Office autosave, save recovery UI and full gameplay regression are outside this step.
- Diff review: production edits limited to the Floor01 composition/capture/restore seam, new component, and writer reservation/cancellation/fence primitives. Existing DTO, quest code, scenes, packages, player, camera, animator and AI are untouched. No deliberate RED scaffold/disabled guard remains in runtime source.
- Verified: git diff --check exit 0; explicit whitespace check includes new untracked code; all five new metadata GUIDs unique across Assets; existing Floor01SaveSystem meta unchanged. `verification.json` records exact checks. All **824 pre-Step6 evidence files** are SHA-256 unchanged. Main project Git status and tracked-diff hash match the pre-step snapshot; its branch remains feature/fo-005-quest-v1.
- Unity normalized ProjectSettings newline bytes during tests. Only a proven newline-only normalization of the initially clean file was restored to configured CRLF; no values changed. Runner now preserves this file along with four existing known Editor-normalized configuration files. Final tracked diff contains only Floor01SaveSystem.cs. All writer/test files created earlier remain untracked as before; nothing was staged.

## Reproduce final targeted checks

Run from `D:/Game3D-01/VT103_Work`:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/VT103/Run-Integration.ps1 -Phase store-check -Platform EditMode -ExpectedTests 11 -TestFilter HUSEscape.Persistence.Autosave.Tests
powershell -NoProfile -ExecutionPolicy Bypass -File tools/VT103/Run-Integration.ps1 -Phase gameplay-check -Platform PlayMode -ExpectedTests 12
```

The runner verifies baseline/branch, refuses an occupied UnityLockfile, uses installed Unity 6000.6.5f1, sets TEMP/TMP/UPM cache and fixtures to D:, imposes a 180 s parent timeout on only its own Editor, keeps raw XML/log/process results, and restores only its captured Editor-normalized file bytes. A timeout/error is not GREEN. The current evidence root is in tools/VT103/current-integration.txt; new runs get new timestamp subfolders.

For manual acceptance, open this worktree's Floor01_Prototype scene in Unity, Play, inspect the automatically attached Floor01Autosave, collect Key_A101, wait until grounded/interaction finishes and verify DraftPath/CommitCount/LastError. Verify F5 still explicitly saves/continues the manual slot. Repeat another quest fact to see revision increase. **Manual keyboard/visual acceptance was not performed.** Do not use the main project to validate this personal slice.

## Files

See changed-files.json for the exact additions/edits of Step6 (old untracked files are not newly claimed here), step6.diff for a complete diff including untracked source, final-source/ and source-hashes.json for the exact tested runtime/tests, runs.json for every actual test outcome. No files deleted.
