"""Reproducible experiments; mutation is committed only in a disposable repository."""
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[2]
EVIDENCE = Path(os.environ.get("VT105_EVIDENCE_DIR", str(ROOT / "artifacts/assignment-vt105")))


def execute(args, cwd=ROOT):
    return subprocess.run(args, cwd=cwd, text=True, capture_output=True)


def record(name, args, cwd=ROOT, expected=0):
    result = execute(args, cwd)
    output = result.stdout + result.stderr + f"\nEXIT_CODE={result.returncode}\n"
    (EVIDENCE / name).write_text(output)
    print(name, "exit", result.returncode)
    if result.returncode != expected:
        raise RuntimeError(output)
    return output


EVIDENCE.mkdir(parents=True, exist_ok=True)
record("03-reverse.log", ["bash", "tools/VT105/run.sh", "--reverse"])
for i in range(1, 4):
    record(f"04-repeat-{i}.log", ["bash", "tools/VT105/run.sh"])
    record(f"05-load-{i}.log", ["bash", "tools/VT105/run.sh", "--load"])

# Demonstrate file-only business configuration change, always restoring the file.
config = ROOT / "tools/VT105/policy.json"
original_config = config.read_bytes()
try:
    config.write_text(json.dumps({"RestoreRetentionHours": 48}) + "\n")
    record("06-config-48.log", ["bash", "tools/VT105/run.sh"])
finally:
    config.write_bytes(original_config)

source = ROOT / "assignments/VT105/SaveSystem/SaveSlotService.cs"
digest_before = hashlib.sha256(source.read_bytes()).hexdigest()
with tempfile.TemporaryDirectory(prefix="vt105-mutation-") as directory:
    temp = Path(directory)
    shutil.copytree(ROOT / "assignments/VT105/SaveSystem", temp / "assignments/VT105/SaveSystem")
    shutil.copytree(ROOT / "tools/VT105", temp / "tools/VT105", ignore=shutil.ignore_patterns("bin", "obj", "__pycache__"))
    for args in (["git", "init", "-b", "mutation/vt105-restore-authorization"],
                 ["git", "config", "user.name", "VT105 temporary mutation experiment"],
                 ["git", "config", "user.email", "vt105@example.invalid"],
                 ["git", "add", "assignments", "tools"],
                 ["git", "commit", "-m", "experiment: unmutated production snapshot"]):
        result = execute(args, temp)
        if result.returncode: raise RuntimeError(result.stderr)
    mutant = temp / "assignments/VT105/SaveSystem/SaveSlotService.cs"
    text = mutant.read_text()
    guard = '''            if (string.IsNullOrWhiteSpace(currentUserId) || slot.UserId != currentUserId) {
                throw new UnauthorizedAccessException($"User {currentUserId} is not authorized to restore slot {slotId}.");
            }'''
    if text.count(guard) != 1: raise RuntimeError("Expected exactly one restore authorization guard")
    mutant.write_text(text.replace(guard, "            // MUTATION: restore authorization removed."))
    diff = execute(["git", "diff", "--", "assignments/VT105/SaveSystem/SaveSlotService.cs"], temp)
    (EVIDENCE / "07-mutation.patch").write_text(diff.stdout)
    for args in (["git", "add", "assignments"], ["git", "commit", "-m", "mutation: remove restore authorization guard"]):
        result = execute(args, temp)
        if result.returncode: raise RuntimeError(result.stderr)
    history = execute(["git", "log", "-2", "--format=%H %s"], temp).stdout
    output = record("07-mutation.log", ["bash", "tools/VT105/run.sh", "--auth-only"], temp, expected=1)
    if "FAIL TC05-cross-owner" not in output: raise RuntimeError("Mutation survived the cross-owner test")
    digest_after = hashlib.sha256(source.read_bytes()).hexdigest()
    (EVIDENCE / "07-mutation-lifecycle.log").write_text(
        "Temporary branch: mutation/vt105-restore-authorization\n" + history +
        "Submission source SHA256 before: " + digest_before + "\nSubmission source SHA256 after: " + digest_after +
        "\nTemporary repository removed at experiment completion.\n")
    assert digest_before == digest_after
record("08-final-clean.log", ["bash", "tools/VT105/run.sh"])
