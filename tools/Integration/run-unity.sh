#!/usr/bin/env bash
# Usage: bash tools/Integration/run-unity.sh /path/to/Unity /tmp/unique-validation-dir
set -euo pipefail
editor=$1
output=$2
repo=$(cd -- "$(dirname -- "$0")/../.." && pwd)
if [[ -e "$output" ]]; then
  echo 'Choose a new output directory; existing projects will not be overwritten.' >&2
  exit 1
fi
mkdir -p "$output"
output=$(cd -- "$output" && pwd)
cp -a "$repo/HUS_Escape/Assets" "$repo/HUS_Escape/Packages" "$repo/HUS_Escape/ProjectSettings" "$output/"
cp "$repo/tools/Integration/"*.cs "$output/Assets/"
# Exit codes/timeouts fail the script. No -quit for the test runner or native probe.
timeout 900 "$editor" -batchmode -nographics -projectPath "$output" -quit -logFile "$output/compile.log"
for platform in EditMode PlayMode; do
  timeout 600 "$editor" -batchmode -nographics -projectPath "$output" -runTests -testPlatform "$platform" -testResults "$output/$platform.xml" -logFile "$output/$platform.log"
  python3 - "$output/$platform.xml" <<'PY'
import sys, xml.etree.ElementTree as ET
root = ET.parse(sys.argv[1]).getroot()
print(sys.argv[1], root.attrib)
assert root.get('result') == 'Passed' and int(root.get('failed', '0')) == 0
PY
done
timeout 300 "$editor" -batchmode -nographics -projectPath "$output" -executeMethod JourneyIntegrationValidation.Run -logFile "$output/native.log"
cat "$output/journey-integration-result.txt"
