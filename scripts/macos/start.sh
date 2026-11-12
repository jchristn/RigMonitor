#!/usr/bin/env bash
# Start RigMonitor on macOS. Always builds first, then starts it, restarting
# a running instance so it runs the new build. If the LaunchAgent from
# install-at-startup.sh is installed, the restart goes through launchctl.
set -euo pipefail
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
. "$SCRIPT_DIR/../common.sh"
. "$SCRIPT_DIR/launchd.sh"
rm_parse_start_args "$@"

if [ "$RM_FOREGROUND" = "1" ]; then
  rm_start_foreground
fi

if rm_launchd_installed; then
  rm_load_env
  rm_build
  if rm_launchd_loaded; then
    rm_log "Restarting $RM_LAUNCHD_LABEL"
    launchctl kickstart -k "$RM_LAUNCHD_DOMAIN/$RM_LAUNCHD_LABEL"
  else
    rm_log "Loading $RM_LAUNCHD_LABEL"
    launchctl bootstrap "$RM_LAUNCHD_DOMAIN" "$RM_LAUNCHD_PLIST"
  fi
  rm_print_urls
  exit 0
fi

rm_start_direct
