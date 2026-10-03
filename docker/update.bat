@echo off
rem Pull the latest published images and recreate the stack. Named volumes are preserved.
cd /d "%~dp0"
docker compose pull --ignore-buildable
docker compose down
docker compose up -d
docker ps -a
