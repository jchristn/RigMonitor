#!/usr/bin/env bash
# Start RigMonitor on Linux. Always builds first, then starts it, restarting
# a running instance so it runs the new build. If the systemd unit from
# install-at-startup.sh is installed, the restart goes through systemctl.
set -euo pipefail
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
. "$SCRIPT_DIR/../common.sh"
rm_parse_start_args "$@"

if [ "$RM_FOREGROUND" = "1" ]; then
  rm_start_foreground
fi

if rm_systemd_installed; then
  rm_load_env
  rm_build
  rm_log "Restarting $RM_SYSTEMD_UNIT"
  rm_sudo systemctl restart "$RM_SYSTEMD_UNIT"
  rm_print_urls
  exit 0
fi

rm_start_direct
