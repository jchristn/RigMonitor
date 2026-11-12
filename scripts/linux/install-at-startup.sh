#!/usr/bin/env bash
# Install RigMonitor as a systemd service that starts at boot and runs as the current user.
set -euo pipefail
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
. "$SCRIPT_DIR/../common.sh"

rm_systemd_available || rm_die "systemd is not running on this host"

rm_build
rm_write_env_file

if pid="$(rm_running_pid)" && ! systemctl is-active --quiet "$RM_SYSTEMD_UNIT" 2>/dev/null; then
  rm_log "Stopping manually started instance (PID $pid) before handing off to systemd"
  rm_stop_direct
fi

rm_systemd_install "$SCRIPT_DIR/start.sh"
rm_sudo systemctl restart "$RM_SYSTEMD_UNIT"
rm_log "RigMonitor will now start at boot"
rm_log "Status: systemctl status $RM_SYSTEMD_UNIT"
rm_log "Logs:   journalctl -u $RM_SYSTEMD_UNIT -f"
rm_print_urls
