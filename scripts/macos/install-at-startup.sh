#!/usr/bin/env bash
# Install a LaunchAgent so RigMonitor starts when you log in, and start it now.
set -euo pipefail
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
. "$SCRIPT_DIR/../common.sh"
. "$SCRIPT_DIR/launchd.sh"

RM_REBUILD=0
rm_ensure_built
rm_write_env_file
mkdir -p "$RM_LOG_DIR" "$(dirname "$RM_LAUNCHD_PLIST")"

if rm_launchd_loaded; then
  launchctl bootout "$RM_LAUNCHD_DOMAIN/$RM_LAUNCHD_LABEL" || true
  sleep 1
fi
if pid="$(rm_running_pid)"; then
  rm_log "Stopping manually started instance (PID $pid) before handing off to launchd"
  rm_stop_direct
fi

cat > "$RM_LAUNCHD_PLIST" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>Label</key>
  <string>$RM_LAUNCHD_LABEL</string>
  <key>ProgramArguments</key>
  <array>
    <string>/bin/bash</string>
    <string>$(rm_xml_escape "$SCRIPT_DIR/start.sh")</string>
    <string>--foreground</string>
  </array>
  <key>WorkingDirectory</key>
  <string>$(rm_xml_escape "$RM_ROOT")</string>
  <key>RunAtLoad</key>
  <true/>
  <key>KeepAlive</key>
  <dict>
    <key>SuccessfulExit</key>
    <false/>
  </dict>
  <key>ThrottleInterval</key>
  <integer>10</integer>
  <key>StandardOutPath</key>
  <string>$(rm_xml_escape "$RM_CONSOLE_LOG")</string>
  <key>StandardErrorPath</key>
  <string>$(rm_xml_escape "$RM_CONSOLE_LOG")</string>
</dict>
</plist>
PLIST

plutil -lint "$RM_LAUNCHD_PLIST" >/dev/null
launchctl bootstrap "$RM_LAUNCHD_DOMAIN" "$RM_LAUNCHD_PLIST"
rm_log "Installed LaunchAgent: $RM_LAUNCHD_PLIST"
rm_log "RigMonitor will now start at login"
rm_log "Console log: $RM_CONSOLE_LOG"
case "$RM_ROOT" in
  "$HOME/Documents"*|"$HOME/Desktop"*|"$HOME/Downloads"*)
    rm_log "Note: the repo is in a privacy-protected folder; if it fails to start, grant /bin/bash Full Disk Access or move the repo." ;;
esac
rm_print_urls
