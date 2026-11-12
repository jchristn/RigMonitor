#!/usr/bin/env bash
# Remove RigMonitor from WSL2 startup: deletes the Windows Startup shortcut and,
# if installed, disables and stops the systemd unit.
set -euo pipefail
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
. "$SCRIPT_DIR/../common.sh"

rm_require_wsl
rm_windows_startup_remove
if rm_systemd_available; then
  rm_systemd_remove
fi
