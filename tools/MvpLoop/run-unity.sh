#!/usr/bin/env bash
# Usage: bash tools/MvpLoop/run-unity.sh /path/to/Unity /tmp/new-mvp-validation
set -euo pipefail
editor=$1
output=$2
repo=$(cd -- "$(dirname -- "$0")/../.." && pwd)
if [[ -e "$output" ]]; then echo 'Choose a new disposable directory.' >&2; exit 1; fi
mkdir -p "$output"
output=$(cd -- "$output" && pwd)
python3 "$repo/tools/Integration/audit_journey.py"
python3 "$repo/tools/PlayerPause/audit_assets.py"
cp -a "$repo/HUS_Escape/Assets" "$repo/HUS_Escape/Packages" "$repo/HUS_Escape/ProjectSettings" "$output/"
cp "$repo/tools/MvpLoop/"*.cs "$repo/tools/Integration/"*.cs "$repo/tools/PlayerPause/"*.cs "$output/Assets/"
cp "$repo/tools/SessionTransactions/TransactionProbe.cs" "$repo/tools/SessionTransactions/TransactionValidation.cs" "$output/Assets/"
cp "$repo/tools/SessionTransactions/TransactionBackend.cs" "$output/Assets/_Project/Scripts/Core/"
timeout 900 "$editor" -batchmode -nographics -projectPath "$output" -quit -logFile "$output/compile.log"
for platform in EditMode PlayMode; do
  timeout 600 "$editor" -batchmode -nographics -projectPath "$output" -runTests -testPlatform "$platform" -testResults "$output/$platform.xml" -logFile "$output/$platform.log"
  python3 - "$output/$platform.xml" <<'PY'
import sys, xml.etree.ElementTree as E
root=E.parse(sys.argv[1]).getroot()
print(root.attrib)
assert root.get('result')=='Passed' and int(root.get('failed','0'))==0
PY
done
for method in MvpLoopValidation.Run JourneyIntegrationValidation.Run PlayerPauseValidation.Run TransactionValidation.Run; do
  timeout 420 "$editor" -batchmode -nographics -projectPath "$output" -executeMethod "$method" -logFile "$output/$method.log"
done
cat "$output/mvp-scene-audit.txt" "$output/mvp-loop-results.txt" "$output/journey-integration-result.txt" "$output/player-pause-results.txt" "$output/transaction-results.txt"
