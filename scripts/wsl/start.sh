#!/usr/bin/env bash
# Start RigMonitor inside WSL (WSL1 or WSL2, no systemd required).
# Builds on first run (or with --rebuild).
set -euo pipefail
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
. "$SCRIPT_DIR/../common.sh"
rm_parse_start_args "$@"

rm_start_direct
if [ "$RM_FOREGROUND" = "0" ] && [ "$RM_BUILD_ONLY" = "0" ]; then
  rm_log "Note: WSL may shut the distro down once no terminal is open. Use install-at-startup.sh to keep it running."
fi
