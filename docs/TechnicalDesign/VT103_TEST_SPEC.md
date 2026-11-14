# VT-103 — baseline and test-first specification

Student: Nguyễn Hữu Duy — 23001853 — SE2026-T26.
Baseline: f933595ef7d1aa810032ddd54cafaad4a438aea4.
Worktree: D:/Game3D-01/VT103_Work.
Branch: individual/23001853-vt103-autosave.
Main Gate working-tree changes are excluded; the main worktree must not change.

## Requirement source and scope

The change request requires automatic drafts and prevents older data from replacing newer data.
The user-approved first slice is Floor01 draft writing only, separate from all three manual slots.
Ten seconds is NOT a lecturer requirement. Timer/lifecycle/UI/Scene integration, mutation and performance are not implemented in this step.

Freshness is a positive logical revision allocated at capture, never completion time, arrival order, wall clock, or percentage of completed quests.
For one draft path, successful durable revisions never decrease and payload must match that revision.
A request ID identifies one revision and one payload. Reusing the ID for a different request is a conflict.

AC1: A valid higher revision is durably committed with its matching payload.
AC2: A lower revision is explicitly rejected as stale and cannot change committed bytes.
AC3: Retrying an already committed ID/revision/payload acknowledges Duplicate without another commit.
AC4: After concurrent valid requests complete, the file contains the maximum revision; intermediate successful commits must not decrease revision.
AC5: Invalid requests and pre-commit failures preserve the last committed draft; manual slots remain untouched.

The names Committed/RejectedStale/Duplicate/Conflict are proposed API acknowledgments of these requirements, not descriptions of the scaffold behavior.
The draft document format is independent formatVersion=1, revision, requestId and opaque Floor01 payload. It is not the existing Floor01SaveData.version.

## Complete reduced decision table

First matching row wins. '-' means irrelevant, not an untested special value.
Ready means stable capture is possible. Valid includes positive revision, identity, payload and format validation.
Identity compares with the committed head. A retry older than the current head is stale; this slice does not promise an unlimited historical idempotency ledger.

| Rule | Ready | Valid | Generation | Stored head | Identity | Candidate revision | Pre-commit I/O | Expected action |
|---|---|---|---|---|---|---|---|---|
| R0 | No | - | - | - | - | - | - | Defer; no request/write |
| R1 | Yes | No | - | - | - | - | - | Reject invalid; preserve file |
| R2 | Yes | Yes | Obsolete | - | - | - | - | Reject obsolete; no write |
| R3 | Yes | Yes | Current | Corrupt/unreadable | - | - | - | Recovery required; no silent replacement |
| R4 | Yes | Yes | Current | Valid | Same ID/revision/payload | Equal | - | Duplicate; no commit |
| R5 | Yes | Yes | Current | Valid | Same ID but changed revision/payload | - | - | Conflict; no commit |
| R6 | Yes | Yes | Current | Missing | - | - | Success | First commit |
| R7 | Yes | Yes | Current | Missing | - | - | Failure | Error; no incomplete principal file |
| R8 | Yes | Yes | Current | Valid | Different ID | Lower | - | Stale; unchanged bytes |
| R9 | Yes | Yes | Current | Valid | Different ID | Equal | - | Conflict; unchanged bytes |
| R10 | Yes | Yes | Current | Valid | Different ID | Higher | Success | Commit candidate revision and payload together |
| R11 | Yes | Yes | Current | Valid | Different ID | Higher | Failure | Error; preserve last commit |

Rows are mutually exclusive after precedence: invalid/obsolete requests need not inspect disk; only valid current requests inspect head; equality splits by identity; only first/newer writes reach I/O.
Post-commit acknowledgment loss is a separate failure boundary: retry must discover the committed identity, not write again.
R0/R2 are future scheduler/lifecycle acceptance rows, intentionally outside the current four-test store contract.

## Test cases (all expected outcomes specified before execution)

P0 = data loss/correctness; P1 = integration/recovery. 'Written' does not mean passed.

| ID | AC / risk | Level | Precondition / input | Expected result | Priority | Step 2 |
|---|---|---|---|---|---|---|---|
| T01 | AC1 / revision and payload mismatch | Unit + real file | Durable r20; submit r21 with different payload | File r21/payload21; Committed | P0 | Written |
| T02 | AC2 / late stale overwrite | Unit + real file; negative | Durable r20; submit r19 | RejectedStale; identical durable bytes | P0 | Written |
| T03 | AC3 / duplicate physical commit | Unit + real file | Seed committed capture-20; retry same request twice while original file denies write/delete sharing | Both Duplicate; no error, rewrite/replace or extra artifact | P0 | Written |
| T04 | AC4 / overlapping writers | Unit + real file | Seed r20; two thread-pool tasks r21/r22, common start barrier | Both terminate; final r22/payload22; r22 Committed, r21 Committed or Stale | P0 | Written |
| T05 | AC1 / first draft absent | Unit | No draft; valid r1 | Create r1 and matching payload | P0 | Planned |
| T06 | AC3 / reused ID, altered data | Unit; negative | Same ID, changed payload or revision | Conflict; unchanged file | P0 | Planned |
| T07 | AC2 / equal revision collision | Unit; negative | Same revision, different ID | Conflict; unchanged file | P0 | Planned |
| T08 | AC5 / malformed request | Unit; negative | Missing ID, invalid revision/payload/schema | Reject invalid; no commit/artifact | P0 | Planned |
| T09 | AC5 / initial write failure | Integration; failure path | No file; deny/throw before commit | Explicit error; no partial principal file | P0 | Planned |
| T10 | AC5 / replace failure | Integration; failure path | Valid existing file; inject I/O failure before replace | Original readable bytes and revision retained | P0 | Planned |
| T11 | AC3 / lost acknowledgment | Integration | Commit succeeds, acknowledgment lost; retry identity | Duplicate; no second commit | P0 | Planned |
| T12 | AC4 / process restart resets revision | Integration | Reopen durable r20 | Next capture revision greater than 20 | P0 | Planned |
| T13 | AC5 / corrupt existing draft | Integration; negative | Corrupt/unreadable principal | Recovery-required; preserve evidence/file | P0 | Planned |
| T14 | AC4 / obsolete lifecycle request | Play Mode | Old request resumes after Continue/New Game generation fence | No obsolete commit into current generation | P0 | Deferred lifecycle |
| T15 | AC5 / unstable gameplay | Play Mode | Door/IK/pickup active at scheduled capture | Defer until stable; no partial snapshot | P1 | Deferred scheduler |
| T16 | AC5 / manual save regression | Play Mode E2E | Hash three manual slots; create/load Floor draft | Manual hashes unchanged; restore existing inventory, no duplicate items | P0 | Deferred integration |
| T17 | AC4 / concurrency SLA | Performance + real file | 120 simultaneous requests to one draft key | Correct maximum revision and p95 <=3.5s; count terminal statuses separately | P1 | Not run in RED |

## Independent oracles and limits

Tests call the actual DraftStore in the production runtime assembly, not a fake store.
Known JSON fixtures seed preconditions directly, avoiding dependence on a working SaveAsync just to reach T02/T03/T04.
Tests read physical file content with JsonUtility as a codec; this is not a duplicated guard implementation.
T03 holds a real Windows FileShare.Read lease: reading is permitted but rewriting/replacing the principal is denied. Duplicate acknowledgment must not require a commit.
T04 uses a common start barrier, no random sleeps. It validates concurrent results/final state, but does NOT prove every possible interleaving or deterministic mutation kill. A controlled storage-boundary schedule and commit-history observation must be added/reviewed for the mutation step.
No claim that these four cases alone exhaust the decision table or prove crash durability.

## RED scaffold interpretation

DraftStore.SaveAsync deliberately returns NotImplemented and never writes. It is not wired into gameplay.
T01/T04 should fail durable revision assertions; T02/T03 should fail required acknowledgment assertions.
T02's byte-preservation check can already hold because the scaffold does nothing. Its RED therefore does NOT demonstrate a reproduced stale overwrite.
Likewise T03 RED alone is not evidence that duplicate commits have already occurred. GREEN and mutation evidence remain future work.
Compilation/discovery errors, fixture failures and timeouts are BLOCKED, not acceptable behavioral RED.

## Reproduction

From D:/Game3D-01/VT103_Work:

    powershell -NoProfile -ExecutionPolicy Bypass -File tools/VT103/Run-Red.ps1

Only HUSEscape.Persistence.Autosave.Tests.DraftStoreRedTests is selected (four tests).
The runner records command.txt, environment.json, editor.log, process.json, results.xml and assertions.json in Evidence/VT103/red-<timestamp>.
Unity Library, UPM/GI cache, TEMP and fixtures stay on D. Fixtures are retained; no cleanup deletes backups or real saves.

Stop after verified behavioral RED. No production fix, timer, mutation, performance run, commit or push is authorized in this step.