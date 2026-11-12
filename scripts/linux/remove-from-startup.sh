#!/usr/bin/env bash
# Remove the RigMonitor systemd service. This also stops the service.
set -euo pipefail
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
. "$SCRIPT_DIR/../common.sh"

rm_systemd_remove
