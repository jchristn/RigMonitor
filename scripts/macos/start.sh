#!/usr/bin/env bash
# Start RigMonitor on macOS. Builds on first run (or with --rebuild).
# If the LaunchAgent from install-at-startup.sh is installed, starts it via launchctl.
set -euo pipefail
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
. "$SCRIPT_DIR/../common.sh"
. "$SCRIPT_DIR/launchd.sh"
rm_parse_start_args "$@"

if [ "$RM_FOREGROUND" = "0" ] && [ "$RM_BUILD_ONLY" = "0" ] && rm_launchd_installed; then
  rm_load_env
  [ "$RM_REBUILD" = "1" ] && rm_build
  if rm_launchd_loaded; then
    rm_log "Starting $RM_LAUNCHD_LABEL"
    launchctl kickstart "$RM_LAUNCHD_DOMAIN/$RM_LAUNCHD_LABEL"
  else
    rm_log "Loading $RM_LAUNCHD_LABEL"
    launchctl bootstrap "$RM_LAUNCHD_DOMAIN" "$RM_LAUNCHD_PLIST"
  fi
  rm_print_urls
  exit 0
fi

rm_start_direct
