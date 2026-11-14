"""Read-only recomputation of recorded samples; creates derived analysis, never edits raw data."""
import csv, json, math, statistics, sys
from collections import Counter
from pathlib import Path

root = Path(sys.argv[1]).resolve()
summaries = json.loads((root / 'summary.json').read_text(encoding='utf-8-sig'))
environment = json.loads((root / 'runtime-environment.json').read_text(encoding='utf-8-sig'))
frequency = environment['stopwatchFrequency']
results = []
lines = ['# VT-103 measured performance (milliseconds)', '',
         'Derived automatically from every measured CSV row. Warm-up excluded. N=120; nearest-rank p95 uses sorted item 114.', '',
         '| Scenario | Round | Started/completed | Counter/timed peak | Min | Mean | Median | p95 | Max | Gate-to-end | Commit/Stale | Outcome |',
         '|---|---:|---|---|---:|---:|---:|---:|---:|---:|---|---|']
for record in summaries:
    file = root / record['SamplesCsv'].replace('\\', '/')
    with file.open(encoding='utf-8-sig', newline='') as f:
        rows = list(csv.DictReader(f))
    assert len(rows) == 120 and len({r['requestId'] for r in rows}) == 120
    assert sorted(int(r['revision']) for r in rows) == list(range(1,121))
    values = sorted(float(r['latencyMs']) for r in rows)
    rank = math.ceil(.95 * len(values))
    actual = dict(MinMs=values[0], MeanMs=statistics.mean(values), MedianMs=statistics.median(values),
                  P95Ms=values[rank-1], MaxMs=values[-1], N=len(values), P95Rank=rank)
    for key, value in actual.items():
        assert math.isclose(value, record['Latency'][key], abs_tol=1e-8), (key, value, record['Latency'][key])
    counts = Counter(r['status'] for r in rows)
    assert sum(counts.values()) == 120
    for key, value in record['StatusCounts'].items():
        assert counts[key] == value
    events = []
    for r in rows:
        begin, end = int(r['startTick']), int(r['endTick'])
        assert end >= begin
        assert math.isclose((end-begin)*1000/frequency, float(r['latencyMs']), abs_tol=1e-8)
        events += [(begin, 1), (end, -1)]
    active = timed_peak = 0
    for _, change in sorted(events):
        active += change
        timed_peak = max(timed_peak, active)
    assert active == 0
    folder = file.parent
    audit = json.loads((folder/'audit.json').read_text())
    correctness = json.loads((folder/'correctness.json').read_text())
    assert correctness['Passed']
    # Independent check of the saved read-back witnesses, keyed to actual reported results.
    witnesses = [o['PriorHead'] for o in audit['preCommitObservations'] if o['PriorHead']] + audit['finalHeads']
    for r in rows:
        if r['status'] == 'Committed':
            assert any(h['Valid'] and h['RequestId'] == r['requestId'] and h['Revision'] == int(r['revision']) for h in witnesses)
    for path in {r['path'] for r in rows}:
        heads = [o['PriorHead'] for o in audit['preCommitObservations'] if o['Path'] == path and o['PriorHead']]
        heads += [h for h in audit['finalHeads'] if h['Path'] == path]
        revisions = [h['Revision'] for h in heads]
        assert revisions == sorted(revisions)
        final = heads[-1]
        assert final['Revision'] == max(int(r['revision']) for r in rows if r['path'] == path)
        assert all(h['Valid'] for h in heads)
    observed = dict(scenario=record['Scenario'], round=record['Round'], recomputed=actual,
                    timedPeak=timed_peak, counterPeak=record['PeakInFlight'], statusCounts=dict(counts),
                    actualRows=len(rows), allCommittedHaveDiskWitness=True, observedHeadMonotonic=True,
                    p95ThresholdMet=actual['P95Ms'] <= 3500, correctness=True)
    results.append(observed)
    f = lambda n: f'{n:.3f}'
    lines.append(f"| {record['Scenario']} | {record['Round']} | {record['Started']}/{record['Completed']} | {record['PeakInFlight']}/{timed_peak} | " +
                 ' | '.join(f(actual[k]) for k in ['MinMs','MeanMs','MedianMs','P95Ms','MaxMs']) +
                 f" | {f(record['GateToCompletionMs'])} | {counts['Committed']}/{counts['RejectedStale']} | {record['Acceptance']} |")
lines += ['', 'Counter peak includes active bookkeeping around the call; timed peak is independently reconstructed from SaveAsync call/result intervals.',
          'P02 does not substitute for P01. P01 has stale rejections; P02 performs 120 commits, so their physical I/O workloads differ.',
          'The .NET 8.0 Windows console uses synthetic 32KiB payloads and read-back auditing; this is not Unity Mono/gameplay performance evidence.']
(root/'independent-recomputation.json').write_text(json.dumps(results, indent=2), encoding='utf-8')
(root/'measured-table.md').write_text('\n'.join(lines)+'\n', encoding='utf-8')
print('\n'.join(lines))