# VT-103 — Floor01 Autosave

Submission: [Pull Request #14](https://github.com/vuvietan170/SE2026.4.GAME-3D-01/pull/14), targeting the team's `dev` branch. Leave the PR unmerged until team review.

## Open the project

Use **Unity 6000.6.5f1** with a valid license. In Unity Hub, add/open the repository's `HUS_Escape` directory and allow package import to finish.

## Run tests

In the Editor, open **Window > General > Test Runner**. Select **EditMode**, then **Run All**; repeat for **PlayMode**.

For recorded batch runs on Windows, use the existing [verification runner](../../tools/VT103/Run-RepairVerification.ps1). It requires `D:\Game3D-01\VT103_Work` and tests the isolated copy at `Temp/VT103Verify`. Follow the copy-preparation instructions in [SUBMISSION.md](SUBMISSION.md) first; ensure that copy matches the submitted source and its Editor is closed before running:

```powershell
Set-Location D:\Game3D-01\VT103_Work
powershell -NoProfile -ExecutionPolicy Bypass -File tools/VT103/Run-RepairVerification.ps1 -Phase submission-edit -Platform EditMode -ExpectedTests 125
powershell -NoProfile -ExecutionPolicy Bypass -File tools/VT103/Run-RepairVerification.ps1 -Phase submission-play -Platform PlayMode -ExpectedTests 45
```

These commands use the runner's existing parameters and expected counts verified against the recorded XML. Choose unused `-Phase` names for subsequent runs: the runner preserves existing evidence and refuses to overwrite a run directory. It records XML, Editor logs and process results under `Evidence/VT103/repair-20261114/<Phase>/`.

## Verified results

| Recorded round | EditMode | PlayMode | Total |
|---|---:|---:|---:|
| `all-editmode` / `all-playmode` | 125/125 | 45/45 | **170/170 PASS** |
| `repeat-edit` / `repeat-play` | 125/125 | 45/45 | **170/170 PASS** |

Both rounds completed with zero failures, zero skipped tests and Unity exit code 0. These are archived executions; this README update does not claim another test run.

## Review materials

- [Submission guide and reproduction details](SUBMISSION.md)
- [Submission file manifest](SUBMISSION_FILES.txt)
- [Root-cause report, per-test results and XML/log evidence](../../Evidence/VT103/repair-20261114/REPORT.md)
- [Report PDF](BT3_23001853_VT-103.pdf) and [editable Markdown](BT3_23001853_VT-103.md); historical scope and limitations are explained in the submission guide.
