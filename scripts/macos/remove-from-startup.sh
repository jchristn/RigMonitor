#!/usr/bin/env bash
# Remove the RigMonitor LaunchAgent. This also stops the launchd-managed instance.
set -euo pipefail
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
. "$SCRIPT_DIR/../common.sh"
. "$SCRIPT_DIR/launchd.sh"

rm_launchd_check_ownership
if rm_launchd_loaded; then
  rm_log "Unloading $RM_LAUNCHD_LABEL"
  launchctl bootout "$RM_LAUNCHD_DOMAIN/$RM_LAUNCHD_LABEL" || true
fi
if rm_launchd_installed; then
  rm -f "$RM_LAUNCHD_PLIST"
  rm_log "Removed $RM_LAUNCHD_PLIST"
else
  rm_log "LaunchAgent is not installed"
fi
rm -f "$RM_PID_FILE"
