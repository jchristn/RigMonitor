#!/usr/bin/env bash
# launchd helpers for the macOS scripts. Sourced, not run directly.
RM_LAUNCHD_LABEL="com.rigmonitor.server"
RM_LAUNCHD_PLIST="$HOME/Library/LaunchAgents/$RM_LAUNCHD_LABEL.plist"
RM_LAUNCHD_DOMAIN="gui/$(id -u)"

rm_launchd_installed() { [ -f "$RM_LAUNCHD_PLIST" ]; }
rm_launchd_loaded() { launchctl print "$RM_LAUNCHD_DOMAIN/$RM_LAUNCHD_LABEL" >/dev/null 2>&1; }

rm_xml_escape() {
  printf '%s' "$1" | sed -e 's/&/\&amp;/g' -e 's/</\&lt;/g' -e 's/>/\&gt;/g'
}
