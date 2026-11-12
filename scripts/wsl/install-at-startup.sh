#!/usr/bin/env bash
# Start RigMonitor in this WSL distro when you log in to Windows.
# Adds a shortcut to your Windows Startup folder that runs start.sh --foreground
# through a hidden PowerShell window, which also keeps the distro alive.
set -euo pipefail
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
. "$SCRIPT_DIR/../common.sh"

rm_require_wsl
rm_build
rm_write_env_file

if pid="$(rm_running_pid)"; then
  rm_log "Stopping running instance (PID $pid) so the startup launcher owns it"
  rm_stop_direct
fi

rm_windows_startup_install start "$SCRIPT_DIR/start.sh" 1
rm_log "RigMonitor will now start when you log in to Windows (distro: $WSL_DISTRO_NAME)"
rm_print_urls
