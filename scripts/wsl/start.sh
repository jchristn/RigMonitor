#!/usr/bin/env bash
# Start RigMonitor inside WSL (WSL1 or WSL2, no systemd required). Always
# builds first, then starts it, restarting a running instance so it runs the
# new build. Uses the Windows startup shortcut when install-at-startup.sh
# installed one.
set -euo pipefail
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
. "$SCRIPT_DIR/../common.sh"
rm_parse_start_args "$@"

if [ "$RM_FOREGROUND" = "1" ]; then
  rm_start_foreground
fi

rm_require_wsl
if rm_windows_startup_installed; then
  # Relaunch through the Windows shortcut so the distro stays alive.
  rm_load_env
  rm_build
  rm_stop_direct
  rm_windows_startup_launch
  rm_print_urls
  exit 0
fi

rm_start_direct
rm_log "Note: WSL may shut the distro down once no terminal is open. Use install-at-startup.sh to keep it running."
