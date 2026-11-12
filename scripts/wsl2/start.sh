#!/usr/bin/env bash
# Start RigMonitor inside WSL2. Builds on first run (or with --rebuild).
# Uses the systemd unit when install-at-startup.sh installed one.
set -euo pipefail
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
. "$SCRIPT_DIR/../common.sh"
rm_parse_start_args "$@"

if [ "$RM_FOREGROUND" = "0" ] && [ "$RM_BUILD_ONLY" = "0" ] && rm_systemd_available && rm_systemd_installed; then
  rm_log "Starting $RM_SYSTEMD_UNIT"
  rm_load_env
  [ "$RM_REBUILD" = "1" ] && rm_build
  rm_sudo systemctl start "$RM_SYSTEMD_UNIT"
  rm_print_urls
  exit 0
fi

rm_start_direct
if [ "$RM_FOREGROUND" = "0" ] && [ "$RM_BUILD_ONLY" = "0" ]; then
  rm_log "Note: WSL2 may shut the distro down once no terminal is open. Use install-at-startup.sh to keep it running."
fi
