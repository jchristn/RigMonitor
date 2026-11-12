#!/usr/bin/env bash
# Stop RigMonitor on macOS. If the LaunchAgent is loaded it is unloaded
# (it loads again at next login, or with start.sh).
set -euo pipefail
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
. "$SCRIPT_DIR/../common.sh"
. "$SCRIPT_DIR/launchd.sh"

if rm_launchd_loaded; then
  rm_log "Unloading $RM_LAUNCHD_LABEL"
  launchctl bootout "$RM_LAUNCHD_DOMAIN/$RM_LAUNCHD_LABEL" || true
  i=0
  while rm_running_pid >/dev/null && [ $i -lt 30 ]; do sleep 1; i=$((i + 1)); done
fi

rm_stop_direct
