#!/usr/bin/env bash
# Pull the latest published images and recreate the stack. Named volumes are preserved.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"
docker compose pull --ignore-buildable
docker compose down
docker compose up -d
docker ps -a
