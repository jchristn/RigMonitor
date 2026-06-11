#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"

echo "Cleaning RigMonitor runtime files from \"$ROOT\""

rm -f -- "$ROOT/rigmonitor.json"

find "$ROOT" -type f \( \
  -name "*.db" -o \
  -name "*.db-*" -o \
  -name "*.sqlite" -o \
  -name "*.sqlite-*" -o \
  -name "*.sqlite3" -o \
  -name "*.sqlite3-*" -o \
  -name "*.log" \
\) -delete

find "$ROOT" -depth -type d \( -name "logs" -o -name "log" \) -exec rm -rf -- {} +

echo "RigMonitor runtime cleanup complete."
