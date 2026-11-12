@echo off
rem Start RigMonitor when you log in to Windows, and build and start it now.
rem Adds a shortcut to start.bat in your Startup folder (no admin rights needed),
rem so it also rebuilds at each login.
setlocal
call "%~dp0common.bat" require-not-admin || exit /b 1

call "%~dp0start.bat" || exit /b 1

set "RM_START_BAT=%~dp0start.bat"
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command ^
  "$ErrorActionPreference='Stop';" ^
  "$lnk = Join-Path ([Environment]::GetFolderPath('Startup')) $env:RM_SHORTCUT_NAME;" ^
  "$s = (New-Object -ComObject WScript.Shell).CreateShortcut($lnk);" ^
  "$s.TargetPath = $env:RM_START_BAT;" ^
  "$s.WorkingDirectory = $env:RM_ROOT;" ^
  "$s.WindowStyle = 7;" ^
  "$s.Description = 'Start RigMonitor';" ^
  "$s.Save();" ^
  "Write-Output ('[rigmonitor] Installed startup shortcut: ' + $lnk)"
if errorlevel 1 exit /b 1

echo [rigmonitor] RigMonitor will now start when you log in
exit /b 0
