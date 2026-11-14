# VT-103 — Step 4: who tests the tests?

Nguyễn Hữu Duy — 23001853 — SE2026-T26.
Worktree: D:/Game3D-01/VT103_Work; branch individual/23001853-vt103-autosave.
Baseline f933595ef7d1aa810032ddd54cafaad4a438aea4.
Evidence root: Evidence/VT103/mutation-20261113-175040/.

## A. M1 — idempotency guard removal

Temporary changes only: remove the observed-ID conflict rejection and same-head identity/duplicate handling. Keep lower-revision rejection and equal-revision/different-ID conflict semantics, but allow same-ID retries to fall through to real persistence.
T03 was strengthened with an unlocked duplicate acknowledgment before its existing read-only sharing lease. This avoids treating an unhandled OS sharing exception as mutation detection; the guarded-original run first confirmed 4/4 PASS.

Actual M1 result: KILLED, one executed test, one assertion failure, zero errors; Unity exit 2.
Test: VT103_T03_RetrySameIdentityAcknowledgesDuplicateWithoutAnotherCommit.
Expected Duplicate; actual Committed. The principal was rewritten by the mutant; this is a contract assertion failure, not a compile/environment failure.
Evidence: M1/mutation.diff, assertions.json, results.xml, editor.log, process.json and restoration.json.
Before/after source SHA256 both 90D1873A0860996C3B040A6C8F66989E7B29F248C55A5A360881966AC4681D0F.

## B. M2 — concurrency guard removal

Mutation: replace state.Gate.WaitAsync() with Task.CompletedTask and remove semaphore Release calls. Keep revision, identity, fingerprint and physical commit decisions otherwise unchanged.

First experiment, original T04: INCONCLUSIVE, not killed and not survived. The two real replacements raced and Task.WaitAll surfaced an AggregateException/IOException, NUnit label Error. No assertion proved stale overwrite. See M2-original/.
The original GREEN source was restored exactly before adding any test seam.

A small instance-scoped internal DraftIoHooks seam was then added:
- AdmissionObserved exposes the existing admission task readiness for scheduling only.
- BeforeCommit executes after real staging and Flush(true), before real Move/Replace.
- Default public constructor passes null hooks. No fake store/backend, dependency or gameplay object.
- InternalsVisibleTo grants only the existing VT103 test assembly access.

Controlled schedule: pause older request21 at the physical pre-replace boundary. Start request22. If admitted, drain its real write before releasing request21. If its admission is pending under the correct guard, release request21 first and then drain both. Expected disk state is ALWAYS revision22; task readiness changes only the schedule, not the oracle. No Thread.Sleep or random delay.

Actual controlled M2: KILLED, one assertion failure, no errors, Unity exit 2, in both M2-controlled and M2-controlled-trace.
Test: VT103_T08_ControlledInterleavingCannotRegressRevision.
Assertion: expected 22, actual 21.
Persisted schedule.txt in M2-controlled-trace/test-data records:

    Newer completed: Committed
    Durable before old release: 22
    Durable after both writes: 21

This directly demonstrates an older prepared request replacing newer durable data.

## C. Test oracles and traceability

- T02: lower revision => RejectedStale and identical committed bytes.
- T03: exact retries => Duplicate; observed historical ID reuse with altered content => Conflict; genuinely older retry => Stale. Added unlocked duplicate check catches M1 with an assertion.
- T05_SameIdentityDifferentPayloadIsConflict: same head ID with altered payload at equal/higher revision => Conflict, identical bytes.
- T06_SameRevisionDifferentIdentityIsConflict: equal revision/different ID => Conflict, identical bytes.
- T07_InjectedCommitFailurePreservesHeadAndRetry: failure-path below.
- T08_ControlledInterleavingCannotRegressRevision: durable max revision invariant under a controlled interleaving, not a semaphore implementation assertion.

T05-T08 are the executable method IDs in this Step 4 suite. Their requirements map to planned identity-conflict, equal-revision-conflict, replace-failure and concurrent-write cases in VT103_TEST_SPEC.md; they do not imply that other planned scheduler/gameplay cases were executed.

## D. Failure path

T07 throws a controlled IOException at BeforeCommit after confirming a real .pending file exists.
Assertions verify no Committed acknowledgment, unchanged principal bytes, no leftover staging file in that fixture, and successful subsequent retry with correct revision21. This exercises the production task, finally cleanup and shared gate, not a fake persistence implementation.
It does not claim to test every OS failure or abrupt power loss.

## E. Restoration and healthy runs

- Each experiment saves source bytes/hash and restores them in finally, including unsuccessful/inconclusive experiments.
- M1 and original M2 restore the exact initial Step 3 GREEN source hash above.
- The legitimate I/O seam changes production source; guarded-with-seam/ stores its correct reference and hashes.
- Both controlled M2 runs restore SHA256 D5E76AAF58F7727660E734A695C8A7A8A5B96A8C3A611434B324D6BFAF067D80 exactly.
- guarded-expanded: 8/8 PASS, exit 0.
- restored-final, AFTER the final mutation: 8/8 PASS, exit 0. T05/T06 negatives and T07 failure path pass; T08 trace ends at 22.
- final-guarded-hash-check.json confirms current DraftStore.cs and DraftIoHooks.cs match the guarded reference. No deliberate mutant remains in production; mutant source exists only as evidence text/diffs outside Assets.

## F. Flaky-test review

1. Concurrency/scheduling: a simultaneous-start barrier alone did not force the data-loss schedule; original M2 produced an unhandled replacement error. T08 uses explicit events/admission observation and drains tasks in finally. Deadlines are harness safety limits, never a successful race oracle. Controlled M2 produced the expected numeric assertion twice; guarded T08 passed in both full eight-test runs.
2. Shared state/execution order: every test creates a fresh GUID directory; identical request IDs therefore belong to different canonical paths. Hooks are instance-scoped, not global mutable backend state. No test relies on another test's save. Event handles are disposed only after requests drain; the failure hook cannot affect the normal retry instance.
3. Runner shutdown limit: one optional additional T08 repeat (stability-1) wrote NUnit XML Passed but the Editor stalled at Cleanup mono/CodeReloadManager shutdown for more than 180 seconds. Only verified test Editor PID 28948 was stopped; process exit -1. Raw outcome FAIL is retained and is NOT counted as a clean PASS or mutation kill. The following queued stability-2 run did not start. No cause in test synchronization is established; Editor shutdown root cause is unknown. See external-shutdown-termination.json. This does not change restored-final's prior 8/8, exit-0 evidence, and no mutation occurred after it.

Limited repetition is not a statistical proof that tests can never flake. No retries were used to relabel the original M2 error as a kill.

## G. Evidence and reproduction

Every case records executed source/hash, mutation.diff, exact Unity command/filter, environment, editor log, NUnit XML, assertion summary, process exit/timeout, outcome and source restoration hash. experiments-summary.json aggregates the observed outcomes without rewriting them.

From the personal worktree, choose a NEW evidence root (existing cases are never overwritten):

    $e = 'Evidence/VT103/mutation-' + (Get-Date -Format yyyyMMdd-HHmmss)
    $f = 'HUSEscape.Persistence.Autosave.Tests.DraftStoreRedTests'
    & tools/VT103/Run-MutationCase.ps1 -EvidenceRoot $e -CaseName M1 -Mutation M1 -Filter "$f.VT103_T03_RetrySameIdentityAcknowledgesDuplicateWithoutAnotherCommit" -ExpectedTests 1
    & tools/VT103/Run-MutationCase.ps1 -EvidenceRoot $e -CaseName M2-controlled -Mutation M2 -Filter "$f.VT103_T08_ControlledInterleavingCannotRegressRevision" -ExpectedTests 1
    & tools/VT103/Run-MutationCase.ps1 -EvidenceRoot $e -CaseName restored -ExpectedTests 8

Runner restores guarded source in finally. Expected mutant Unity exit 2 is recorded; a wrapper's successful completion means the experiment was collected, not that mutant tests passed. No XML/compile errors/timeouts are classified as killed.
Run-Green.ps1 now defaults to the expanded eight-test VT103 suite.

## H. Scope and preservation

Changed: DraftStore.cs (legitimate internal seam only), DraftStoreRedTests.cs (T03 assertion plus T05-T08), Run-Green.ps1 (eight-test count).
Added: DraftIoHooks.cs/.meta, Run-MutationCase.ps1, this report, and mutation evidence. No gameplay integration or Scene changes.
Original GREEN backups and hashes are in original-green/. All 68 prior RED/GREEN artifacts and all ten main-worktree uncommitted file hashes remain identical (protection.json).
No tracked baseline files remain modified, no staged changes, no commit/push/merge/PR/reset/clean/stash. Unity project/cache/fixtures remain on D. Editor main process was not stopped or modified.
Scope still one process and Windows canonical path; no cross-process locking. No 120-request benchmark, autosave gameplay, PDF or full regression suite. STOP AT STEP 4.