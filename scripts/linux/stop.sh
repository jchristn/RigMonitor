#!/usr/bin/env bash
# Stop RigMonitor on Linux (via systemctl if the startup unit is installed).
set -euo pipefail
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
. "$SCRIPT_DIR/../common.sh"

if rm_systemd_installed && systemctl is-active --quiet "$RM_SYSTEMD_UNIT"; then
  rm_log "Stopping $RM_SYSTEMD_UNIT"
  rm_sudo systemctl stop "$RM_SYSTEMD_UNIT"
  rm -f "$RM_PID_FILE"
  rm_log "Stopped"
  exit 0
fi

rm_stop_direct
