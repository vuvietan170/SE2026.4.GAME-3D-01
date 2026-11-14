# VT-103 — Step 2: observed RED evidence

Nguyễn Hữu Duy — 23001853 — SE2026-T26.
Baseline: f933595ef7d1aa810032ddd54cafaad4a438aea4.
Branch: individual/23001853-vt103-autosave.
Worktree: D:/Game3D-01/VT103_Work.

## Actual final run

- Unity 6000.6.5f1, NUnit Edit Mode, exact fixture filter DraftStoreRedTests.
- Evidence: Evidence/VT103/red-20261113-170710/.
- Executed 4, passed 0, failed 4, skipped 0. All four are assertion failures.
- Unity process exit code 2; no timeout; process runtime 72.7949181 seconds.
- This runtime includes fresh-project imports and is NOT a performance benchmark.
- Production DraftStore.SaveAsync still returns NotImplemented. No saving algorithm, guard, timer or gameplay hook was added.

| Test suffix | Expected | Observed assertion |
|---|---|---|
| VT103_T01_NewerRevisionPersistsItsOwnPayload | Durable r21 / payload21 | Revision remained 20, expected 21 |
| VT103_T02_LateOlderRequestIsExplicitlyRejectedWithoutOverwrite | RejectedStale, identical bytes | Bytes unchanged; status NotImplemented instead of RejectedStale |
| VT103_T03_RetrySameIdentityAcknowledgesDuplicateWithoutAnotherCommit | Duplicate twice, no new commit | NotImplemented twice instead of Duplicate |
| VT103_T04_TwoConcurrentRequestsLeaveMaximumRevisionOnDisk | Durable r22 / payload22 | Revision remained 20, expected 22 |

T02/T03 do not demonstrate an observed stale overwrite or duplicate physical write: the no-op scaffold does neither. They demonstrate missing required behavior/acknowledgment. T04's start barrier is not a proof of every interleaving or a deterministic mutation kill; that remains a later review checkpoint.

## Reproduction and evidence

    Set-Location D:\Game3D-01\VT103_Work
    powershell -NoProfile -ExecutionPolicy Bypass -File tools/VT103/Run-Red.ps1

Runner selects only HUSEscape.Persistence.Autosave.Tests.DraftStoreRedTests.
See command.txt for actual Unity arguments; environment.json for D: cache/fixture paths;
results.xml for NUnit discovery/assertions/stacks; assertions.json for compact test outcomes;
process.json for exit code and timeout; editor.log for compilation and execution.
Raw assertions/results were not edited. Source hashes are in source-hashes.json.

Earlier attempts red-20261113-170445 and red-20261113-170642 were compilation BLOCKED,
not behavioral RED: this Unity NUnit lacks NonParallelizable and Is.AnyOf. Only test
compatibility syntax was changed; expected outcomes and production scaffold were unchanged.

## Preservation and file scope

- Main project remains feature/fo-005-quest-v1. All ten original uncommitted file hashes match the pre-worktree snapshot.
- Evidence/VT103/baseline-and-main-protection.json records initial status/hash proof.
- No tracked baseline source, Scene, package or ProjectSettings file remains changed in the personal worktree.
- Fresh Editor import normalized line endings in four existing settings assets. Their content was verified unchanged after line-ending normalization; editor bytes were archived and exact checkout bytes restored. See editor-import-restoration.json. No settings feature change was made.
- No code/file deleted, staged, committed, pushed or merged. No branch switch in main.
- Unity Library/TEMP/GI/UPM and fixture directories are on D. No Unity project/Library on C was created.

New source files:
- Assets/_Project/Scripts/Utilities/Autosave/DraftContract.cs
- Assets/_Project/Scripts/Utilities/Autosave/DraftStore.cs
- Assets/_Project/Scripts/Utilities/Autosave/HUSEscape.Persistence.Autosave.asmdef
- Assets/_Project/Scripts/Utilities/Autosave/Tests/Editor/DraftStoreRedTests.cs
- Assets/_Project/Scripts/Utilities/Autosave/Tests/Editor/VT103.Autosave.EditModeTests.asmdef
- Eight new .meta files for five assets and three new folders; existing GUIDs retained.
- tools/VT103/Run-Red.ps1
- docs/TechnicalDesign/VT103_TEST_SPEC.md
- This report and Evidence/VT103 artifacts/fixtures.

## Decisions/checkpoints before GREEN

1. Confirm guard scope: same instance only is insufficient if two stores can target the same path. Recommend all same-path writers in one process; inter-process support is not yet specified or tested.
2. Confirm equal revision/different identity and reused ID/changed content are conflicts; retries older than the current head may return Stale rather than retaining an unlimited historical ID ledger.
3. Approve a controlled real-I/O scheduling seam for a deterministic concurrency-guard mutation test; a common start barrier alone cannot establish mutation sensitivity.
4. Failure recovery, missing/corrupt files, restart high-water revision and integration remain planned, not executed. No claim of completed autosave or crash durability.

Stop at RED. No GREEN patch, mutation, 120-task benchmark or old Unity regression suite was run.