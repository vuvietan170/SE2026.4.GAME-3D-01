# VT-103 — Step 3: RED to GREEN

Nguyễn Hữu Duy — 23001853 — SE2026-T26.
Worktree: D:/Game3D-01/VT103_Work.
Branch: individual/23001853-vt103-autosave.
Baseline: f933595ef7d1aa810032ddd54cafaad4a438aea4.

## A. Files changed relative to Step 2

Modified:
- HUS_Escape/Assets/_Project/Scripts/Utilities/Autosave/DraftContract.cs
- HUS_Escape/Assets/_Project/Scripts/Utilities/Autosave/DraftStore.cs
- HUS_Escape/Assets/_Project/Scripts/Utilities/Autosave/Tests/Editor/DraftStoreRedTests.cs

Added:
- tools/VT103/Run-Green.ps1 (Run-Red.ps1 retained unchanged)
- This report and new Evidence/VT103/green-* artifacts.

No tracked baseline file, Scene, gameplay system, package or ProjectSettings remains changed.
Existing metadata/assembly definitions retained; no package added. No autosave timer, capture scheduler or gameplay integration.

## B. Production design and contract

- Canonical Windows key: Path.GetFullPath, normalized separators and OrdinalIgnoreCase.
- Per-path state shares a SemaphoreSlim AND observed committed identity records across all DraftStore instances in ONE process.
- Reading/validating head, deciding revision/identity and replacing principal happen under the same gate.
- An incoming revision below head is Stale before identity checks, including old retries.
- Same head revision, ordinal request ID, SHA-256 UTF-8 fingerprint AND exact payload is Duplicate without staging or writing.
- Equal revision/different ID is Conflict. Reusing an observed committed ID with a different revision or payload is Conflict, including after head advances within this process.
- Faulted/uncommitted attempts do not reserve identity. Identity records are added only for valid observed heads and after successful commits.
- A request computes its own fingerprint. A stored non-null fingerprint must match stored payload. The pre-patch V1 fixtures lacking this optional field remain readable without rewriting duplicates.
- Unique same-directory staging, Flush(true), then File.Replace (or File.Move on first creation). No delete-then-move fallback.
- Committed is returned only after the filesystem operation succeeds; validation/serialization/I/O errors fault the task. Cleanup can remove only the request-owned scratch file.
- Constructor rejects the three manual save filenames. Actual test I/O stays in isolated D: fixtures.

Step 3 clarification overrides the ambiguous precedence in the older planned decision table: a revision below head is always Stale, even for a reused ID. The within-process identity history now handles reuse above the head too. Persistent history across process restarts is not implemented; after restart only IDs represented by the durable head are initially known.

## C. Actual test results

Unity 6000.6.5f1; NUnit Edit Mode; exact DraftStoreRedTests fixture filter only.
FINAL evidence: Evidence/VT103/green-20261113-173233/.
Actual: 4 executed, 4 Passed, 0 Failed, 0 Skipped; exit code 0; timeout false.
Process time 11.415262 seconds is NOT a 120-request performance benchmark.

| Test | Result | Oracle |
|---|---|---|
| T01_NewerRevisionPersistsItsOwnPayload | PASS | Durable revision21/payload21, Committed |
| T02_LateOlderRequestIsExplicitlyRejectedWithoutOverwrite | PASS | Stale revision19; identical principal bytes |
| T03_RetrySameIdentityAcknowledgesDuplicateWithoutAnotherCommit | PASS | Two Duplicates under read-only sharing lease; no second commit/artifact; after head advances, reused historical ID with altered payload conflicts and leaves bytes unchanged; old genuine retry is Stale |
| T04_TwoConcurrentRequestsLeaveMaximumRevisionOnDisk | PASS | Two store instances and normalized dot/case path alias; barrier; final revision22/payload22 and valid statuses |

Original expected assertions were not weakened. T04 setup was strengthened to two path-equivalent instances. T03 was strengthened from the explicit Step 3 request-identity contract. Test names/count remain exactly four.

## D. Evidence and reproduction

    Set-Location D:\Game3D-01\VT103_Work
    powershell -NoProfile -ExecutionPolicy Bypass -File tools/VT103/Run-Green.ps1

Final evidence contains command.txt, environment.json, process.json, results.xml, assertions.json, editor.log, red-to-green.diff, source-review-hashes.json and verified-red-source/.
Before-patch copies were verified against the ORIGINAL RED source-hashes.json. They are stored only in new GREEN evidence; original RED artifacts are not changed.

Execution history, all preserved:
1. green-20261113-172430: original four tests passed.
2. AI review identified missing historical-ID coverage. Added a requirement-derived assertion to T03; green-20261113-173132 genuinely FAILED (3 passed, 1 failed, exit 2): expected Conflict, actual Committed. This was a regression discovery, not a guard-removal mutation.
3. Added shared in-process identity memory; green-20261113-173233 passed all four strengthened tests.
A directory prefix alone never determines PASS: use the actual XML/process records.

## E. Self diff review after the final tests

- Scope: only draft contract/store, directly related T03/T04 setup/assertions and GREEN runner/report/evidence. No manual save migration or game integration.
- Design: BCL persistence assembly; no Unity APIs on workers and no global gameplay singleton. Shared path state is synchronization, not a service locator.
- Correctness: gate covers read/check/commit and identity memory; finally releases it on fault; rejection branches precede staging; success follows replace. Duplicate principal-write avoidance is exercised with a real Windows sharing lease.
- Evidence: final NUnit XML and process exit 0 agree; no C# compilation errors in the final log. Source diff is against verified RED texts because Step 2 files are still uncommitted. No mutation, performance or whole-game regression suite run.

## F. AI review (same assistant, after tests; not an independent reviewer)

CONFIRMED review finding, now fixed: the initial implementation compared IDs only with the current head. Reusing committed capture-20 with altered payload/revision22 after capture-21 was accepted. The added T03 assertion demonstrated expected Conflict / actual Committed in green-20261113-173132. Shared per-path committed-ID memory fixed the defect; the same assertion passes in the final run.

CONFIRMED remaining coverage gap: T04 observes final disk state but does not record intermediate commit history or force every read/check/replace interleaving. Its GREEN result does not establish deterministic mutation sensitivity or general linearizability. Keep controlled-I/O scheduling/history checks for a later authorized mutation step.

INSUFFICIENT-EVIDENCE concern: serialized same-path I/O plus Task.Run might exceed p95 3.5s with 120 concurrent tasks. No such measurement exists. This is a performance hypothesis, not a proven defect; do not claim SLA failure/pass or optimize speculatively.

## G. Remaining risks / not tested

- One process only; no cross-process/distributed lock. Windows lexical/case path normalization does not resolve physical symlink/hardlink aliases or provide a hostile-path security boundary.
- Gate and observed identity records live for the process lifetime. Unlimited distinct paths/request IDs would retain memory. Historical ID memory is not persisted across restarts.
- Only four tests run. One identity-history conflict is now exercised, but remaining conflict variants, corrupt/fingerprint files, first-write/replace fault injection and restart recovery are NOT additional test passes.
- Payload remains opaque text, not a validated full Floor01 gameplay adapter. No timer, lifecycle generation fence or Continue UI added.
- Flush/replace is not proof against every power-loss/filesystem scenario. Cleanup is best effort and can leave a unique pending artifact if access is denied.
- No mutation/120-task benchmark and no commit/push/merge/PR. Stop at GREEN.