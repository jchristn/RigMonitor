#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
CONFIGURATION="${RIGMONITOR_CONFIGURATION:-Debug}"
SERVER_PROJECT="$ROOT/src/RigMonitor.Server/RigMonitor.Server.csproj"
RUN_DIR="$ROOT/.rigmonitor-native"
SERVER_DLL="$RUN_DIR/RigMonitor.Server.dll"
SERVER_EXE="$RUN_DIR/RigMonitor.Server"
SETTINGS_FILE="$ROOT/rigmonitor.json"

echo "RigMonitor native startup"
echo "Root: $ROOT"
echo "Configuration: $CONFIGURATION"
echo

cd "$ROOT/dashboard"
if [ -f package-lock.json ]; then
  npm ci
else
  npm install
fi
npm run build

mkdir -p "$ROOT/data/logs"
mkdir -p "$RUN_DIR"

cd "$ROOT"
dotnet build "$SERVER_PROJECT" -c "$CONFIGURATION" -o "$RUN_DIR"

echo
echo "Starting RigMonitor from \"$ROOT\""
echo "Runtime: \"$RUN_DIR\""
echo "Settings: \"$SETTINGS_FILE\""
echo "Dashboard: http://localhost:9990/dashboard"
echo "OpenAPI:   http://localhost:9990/openapi"
echo "Press Ctrl+C to stop."
echo

if [ -x "$SERVER_EXE" ]; then
  exec "$SERVER_EXE" --settings "$SETTINGS_FILE"
fi

exec dotnet "$SERVER_DLL" --settings "$SETTINGS_FILE"
