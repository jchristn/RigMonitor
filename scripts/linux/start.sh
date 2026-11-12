#!/usr/bin/env bash
# Start RigMonitor on Linux. Builds on first run (or with --rebuild).
# If the systemd unit from install-at-startup.sh is installed, starts it via systemctl.
set -euo pipefail
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
. "$SCRIPT_DIR/../common.sh"
rm_parse_start_args "$@"

if [ "$RM_FOREGROUND" = "0" ] && [ "$RM_BUILD_ONLY" = "0" ] && rm_systemd_installed; then
  rm_log "Starting $RM_SYSTEMD_UNIT"
  rm_load_env
  [ "$RM_REBUILD" = "1" ] && rm_build
  rm_sudo systemctl start "$RM_SYSTEMD_UNIT"
  rm_print_urls
  exit 0
fi

rm_start_direct
