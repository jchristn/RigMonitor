@echo off
rem Remove RigMonitor from Windows login startup.
rem A running instance is left running; use stop.bat to stop it.
setlocal
call "%~dp0common.bat" || exit /b 1

powershell.exe -NoProfile -ExecutionPolicy Bypass -Command ^
  "$lnk = Join-Path ([Environment]::GetFolderPath('Startup')) $env:RM_SHORTCUT_NAME;" ^
  "if (Test-Path -LiteralPath $lnk) { Remove-Item -LiteralPath $lnk -Force; Write-Output ('[rigmonitor] Removed startup shortcut: ' + $lnk) }" ^
  "else { Write-Output '[rigmonitor] RigMonitor is not installed at startup' }"
exit /b 0
