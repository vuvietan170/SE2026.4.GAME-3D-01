#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/../.."
if [[ -n "${DOTNET:-}" ]]; then
    runtime="$DOTNET"
elif command -v dotnet >/dev/null; then
    runtime="$(command -v dotnet)"
else
    runtime="$HOME/Unity/Hub/Editor/6000.6.5f1/Editor/Data/DotNetSdk/dotnet"
fi
export DOTNET_CLI_HOME=/tmp/vt105-dotnet-home
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export DOTNET_NOLOGO=1
"$runtime" run --project tools/VT105/Harness.csproj -- "$@"
