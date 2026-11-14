# VT-103 — Step 5: Performance, coverage and risk evidence

Nguyễn Hữu Duy — 23001853 — SE2026-T26.
Branch individual/23001853-vt103-autosave; baseline f933595ef7d1aa810032ddd54cafaad4a438aea4.
Successful evidence: Evidence/VT103/performance-20261113-182949/.

## A. Harness / environment

Small offline .NET console project tools/VT103/Performance/VT103.Performance.proj, net8.0 Release.
Links the actual DraftStore.cs, DraftContract.cs and DraftIoHooks.cs; no copied/fake writer, no source change, no third-party package.
Uses existing Unity-bundled SDK8.0.318/runtime.NET8.0.21, not the Unity Editor/Mono runtime.
Intel i7-12700H, 14 cores/20 logical processors, Windows build26200, NTFS D: on NVMe INTEL SSDPEKNU512GZ.
Other Unity/Hub processes were running; environment was recorded, not artificially isolated or tuned.
32KiB deterministic ASCII JSON payload per request, unique capture ID/revision. This is an opaque synthetic Floor01 fixture, not a complete gameplay save.

All fresh fixture/build/cache/log paths are on D. No Unity Editor launched or extra Unity project/Library created. MSBuild/C# build succeeded with0 warnings/0 errors.

## B. Tasks and measurement method

One separate120-client warm-up per scenario (240 requests), excluded from all measured statistics.
Three measured rounds per P01/P02,120 clients each:720 measured requests, none discarded.
Every round has120 prepared at the common asynchronous start gate,120 actual SaveAsync calls and120 completions.
P01 uses120 separate store instances sharing one canonical file; observed in-flight peak120 in all three rounds.
P02 uses120 independent paths; observed peak21,20,21 (not120 simultaneous physical writes).
No Thread.Sleep, artificial delay, thread-pool tuning or production optimization.

Stopwatch starts immediately before SaveAsync and stops after its observed result/exception: includes semaphore wait, worker scheduling, serialization, actual file I/O and read-back audit-hook overhead.
Payload construction, store construction, directory setup and pre-call dispatch delay are outside that API latency. Raw offsets, gate-to-last-result time and dispatch spread are separately recorded.
p95 uses nearest rank ceil(.95*120)=114. Median is the mean of the middle two samples.

## C. Recorded results

# VT-103 measured performance (milliseconds)

Derived automatically from every measured CSV row. Warm-up excluded. N=120; nearest-rank p95 uses sorted item 114.

| Scenario | Round | Started/completed | Counter/timed peak | Min | Mean | Median | p95 | Max | Gate-to-end | Commit/Stale | Outcome |
|---|---:|---|---|---:|---:|---:|---:|---:|---:|---|---|
| P01 | 1 | 120/120 | 120/120 | 2.030 | 34.295 | 35.077 | 48.951 | 50.490 | 50.596 | 2/118 | MET |
| P02 | 1 | 120/120 | 21/21 | 2.660 | 11.888 | 6.964 | 45.130 | 62.218 | 93.644 | 120/0 | MET |
| P02 | 2 | 120/120 | 20/20 | 2.447 | 12.167 | 10.328 | 27.498 | 49.303 | 81.209 | 120/0 | MET |
| P01 | 2 | 120/120 | 120/120 | 2.216 | 47.637 | 47.376 | 70.817 | 72.379 | 72.509 | 3/117 | MET |
| P01 | 3 | 120/120 | 120/120 | 2.405 | 60.326 | 62.870 | 105.375 | 106.924 | 107.017 | 7/113 | MET |
| P02 | 3 | 120/120 | 21/21 | 3.109 | 10.906 | 7.407 | 23.328 | 94.212 | 110.929 | 120/0 | MET |

Counter peak includes active bookkeeping around the call; timed peak is independently reconstructed from SaveAsync call/result intervals.
P02 does not substitute for P01. P01 has stale rejections; P02 performs 120 commits, so their physical I/O workloads differ.
The .NET 8.0 Windows console uses synthetic 32KiB payloads and read-back auditing; this is not Unity Mono/gameplay performance evidence.


Raw CSV/JSON has120 rows per measured round. Analyze-Evidence.py independently recomputed every statistic from CSV, checked tick-derived durations, IDs/revisions, statuses, observed read-back witnesses and monotonic sequences. See independent-recomputation.json.

## D. Threshold interpretation

P01: MET for all three observed rounds against3500ms. P02: also MET for its actual independent-path bursts, reported separately and not used as a replacement for P01.
P01 produced2/3/7 Committed and118/117/113 RejectedStale across rounds; P02 produced120 Committed each round. No Duplicate, Conflict, IOError or OtherError in the measured data.
Total measured results:372 Committed,348 RejectedStale,720 completed. Every outcome remains in the raw samples; commit-only statistics are available in summary.json.
The comparison is not an isolated estimate of semaphore cost: P01 and P02 perform different numbers of physical commits.
MET applies to this32KiB, instrumented.NET console workload/environment only. It does NOT prove Unity Mono/gameplay production SLA, larger saves, sustained load, cross-process safety or statistical tail guarantees.

## E. Data correctness after load

Each pre-commit hook reads the REAL prior head while the production gate is held; final files are read after all requests complete.
Every one of372 Committed acknowledgments has a matching actual disk witness (next pre-commit head or final file), including expected identity/revision/payload/fingerprint. No observed head sequence regressed; final head equals highest submitted revision PER PATH and is at least every acknowledged commit for that path.
P01 final revision is120 each round. P02's120 final files per round match their own requests.
Independent Python read-back AFTER the benchmark process exited verified363 final measured files: JSON format, exact deterministic payload, UTF8 size32768, request ID/revision, SHA256 and max acknowledged revision.
No pending files remained. No manual save file was targeted.
This is normal file read-back, not crash/restart recovery, gameplay Continue or proof against sudden power loss.

## F. Line / branch coverage

Line coverage: NOT AVAILABLE. Branch coverage: NOT AVAILABLE.
No supported collector/profiler/CLI was found on PATH, global tool shims, Unity PackageCache/built-in coverage packages or bundled SDK collector paths checked. Microsoft.CodeCoverage.IO.dll exists, but it is not a collection CLI/profiler.
No dependency installation or Unity settings change was performed. Discovery commands/results are recorded in coverage-discovery.command.txt and coverage-availability.json.
No invented percentage; qualitative risk coverage and commit audit hooks are not line/branch coverage.

## G. Risk coverage

See risk-coverage.md for acceptance criterion/risk -> test ID -> file -> actual result -> residual risk.
Earlier NUnit evidence is READ, not rerun: mutation-20261113-175040/restored-final/results.xml,8/8 Passed, process0.
Covered with bounded evidence: newer snapshots(T01), stale rejection(T02), duplicate/history identity(T03), identity conflicts(T05/T06), controlled same-process concurrency(T04/T08 and M2), pre-replace failure/cleanup/retry(T07), and the current P01/P02 workload.
NOT TESTED/unsupported: crash recovery, persisted ID history/revision allocation after restart, cross-process locking, physical path aliases and autosave gameplay/lifecycle integration. No test result is inferred for these risks.

## H. Flaky / clean-run distinctions

See flaky-review.md. Start-barrier scheduling risk was addressed in Step4 by controlled T08; shared state/order is isolated by unique fixture paths and instance hooks.
Prior stability-1 has NUnit1 Passed but process-1 after a shutdown stall; parent timeout=false and external stop documented. It is NOT a clean PASS or mutation kill. Editor hang root cause remains unknown.
Step5's console benchmark process exited0, timeout=false and completed all six measured rounds normally; this does not establish the old Editor hang was fixed. No Unity regression suite or new mutation run.

## I. Evidence, reproduction and preservation

From D:/Game3D-01/VT103_Work:

    powershell -NoProfile -ExecutionPolicy Bypass -File tools/VT103/Performance/Run-Performance.ps1

This creates a NEW performance-<timestamp> folder and refuses to overwrite rounds.
Read-only independent analyses (for a new run, substitute its folder):

    python tools/VT103/Performance/Analyze-Evidence.py Evidence/VT103/performance-20261113-182949
    python tools/VT103/Performance/Verify-Readback.py Evidence/VT103/performance-20261113-182949

Benchmark source snapshot and SHA256 are under source/ and source-hashes.json; analysis script copies/hashes are recorded separately. Restore/build/run commands, stdout/stderr, process records, environment/hardware details, warm-up output,720 raw measured samples, head audits and derived reports are retained.
The three earlier setup directories(-182739,-182810,-182902) contain harness quoting/exit-recording/C#target setup failures, not completed measurements. They are retained and not counted as discarded benchmark results. Only the harness was corrected.

Changes: new tools/VT103/Performance/{Program.cs,VT103.Performance.proj,NuGet.Config,Run-Performance.ps1,Analyze-Evidence.py,Verify-Readback.py}, this report and new performance evidence.
protection.json confirms all14 production/test/metadata file hashes,210 prior evidence files and10 uncommitted main-project files remain identical. No tracked baseline source/settings/package changes; no staged files, commit, push, merge, PR, gameplay integration or PDF.
STOP AT STEP5.