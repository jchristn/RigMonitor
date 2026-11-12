#!/usr/bin/env bash
# Remove the Windows Startup shortcut for this WSL distro.
# A running instance is left running; use stop.sh to stop it.
set -euo pipefail
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
. "$SCRIPT_DIR/../common.sh"

rm_require_wsl
rm_windows_startup_remove
