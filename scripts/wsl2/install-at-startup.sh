#!/usr/bin/env bash
# Start RigMonitor in this WSL2 distro when you log in to Windows.
#
# With systemd enabled ([boot] systemd=true in /etc/wsl.conf): installs a
# systemd unit, plus a Windows Startup shortcut that boots the distro at logon
# and keeps it alive so the unit keeps running.
# Without systemd: the Windows Startup shortcut runs start.sh --foreground.
set -euo pipefail
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
. "$SCRIPT_DIR/../common.sh"

rm_require_wsl
rm_build
rm_write_env_file

if pid="$(rm_running_pid)" && ! { rm_systemd_available && systemctl is-active --quiet "$RM_SYSTEMD_UNIT" 2>/dev/null; }; then
  rm_log "Stopping running instance (PID $pid) so the startup mechanism owns it"
  rm_stop_direct
fi

if rm_systemd_available; then
  rm_systemd_install "$SCRIPT_DIR/start.sh"
  rm_sudo systemctl restart "$RM_SYSTEMD_UNIT"
  rm_windows_startup_install keepalive "$SCRIPT_DIR/start.sh" 1
  rm_log "Logs: journalctl -u $RM_SYSTEMD_UNIT -f"
else
  rm_log "systemd is not enabled in this distro; using the Windows startup launcher only"
  rm_windows_startup_install start "$SCRIPT_DIR/start.sh" 1
fi
rm_log "RigMonitor will now start when you log in to Windows (distro: $WSL_DISTRO_NAME)"
rm_print_urls
