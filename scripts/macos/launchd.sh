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

# Fails when the LaunchAgent (or its folder) belongs to another user, e.g.
# after an earlier run with sudo.
rm_launchd_check_ownership() {
  local dir
  dir="$(dirname "$RM_LAUNCHD_PLIST")"
  if [ -e "$RM_LAUNCHD_PLIST" ] && [ "$(stat -f %u "$RM_LAUNCHD_PLIST")" != "$(id -u)" ]; then
    rm_die "$RM_LAUNCHD_PLIST is owned by $(stat -f %Su "$RM_LAUNCHD_PLIST"); fix with: sudo chown $(id -un):$(id -gn) \"$RM_LAUNCHD_PLIST\""
  fi
  if [ -d "$dir" ] && [ ! -w "$dir" ]; then
    rm_die "$dir is not writable by $(id -un); fix with: sudo chown $(id -un):$(id -gn) \"$dir\""
  fi
}
