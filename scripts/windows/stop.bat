@echo off
rem Stop RigMonitor on Windows. Exits non-zero if a running instance could not be stopped.
setlocal
call "%~dp0common.bat" || exit /b 1

set "RM_PID="
if exist "%RM_PID_FILE%" set /p RM_PID=<"%RM_PID_FILE%"
if defined RM_PID (
    tasklist /FI "PID eq %RM_PID%" /FI "IMAGENAME eq RigMonitor.Server.exe" /NH 2>nul | find "%RM_PID%" >nul || set "RM_PID="
)

if defined RM_PID (
    echo [rigmonitor] Stopping RigMonitor ^(PID %RM_PID%^)
    taskkill /PID %RM_PID% /T /F >nul 2>&1
    if errorlevel 1 (
        echo [rigmonitor] ERROR: could not stop PID %RM_PID%. If it was started as administrator, run stop.bat from an elevated prompt.
        exit /b 1
    )
    if exist "%RM_PID_FILE%" del /q "%RM_PID_FILE%" >nul 2>&1
    echo [rigmonitor] Stopped
    exit /b 0
)

rem Fall back to any RigMonitor.Server.exe started from this repo's runtime directory.
rem Elevated processes hide their path from a normal prompt, so report those instead of guessing.
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command ^
  "$all = @(Get-Process -Name 'RigMonitor.Server' -ErrorAction SilentlyContinue);" ^
  "$mine = @($all | Where-Object { $_.Path -eq $env:RM_SERVER_EXE });" ^
  "$hidden = @($all | Where-Object { -not $_.Path });" ^
  "$failed = $false;" ^
  "foreach ($p in $mine) { Write-Output ('[rigmonitor] Stopping RigMonitor (PID ' + $p.Id + ')'); try { Stop-Process -Id $p.Id -Force -ErrorAction Stop } catch { Write-Output ('[rigmonitor] ERROR: could not stop PID ' + $p.Id + ': ' + $_.Exception.Message); $failed = $true } }" ^
  "if ($hidden.Count -gt 0) { Write-Output ('[rigmonitor] ERROR: RigMonitor.Server.exe is running with higher privileges (PID ' + (($hidden | ForEach-Object { $_.Id }) -join ', ') + '). Run stop.bat from an elevated prompt.'); exit 1 }" ^
  "if ($failed) { exit 1 }" ^
  "if ($mine.Count -eq 0) { Write-Output '[rigmonitor] RigMonitor is not running' } else { Write-Output '[rigmonitor] Stopped' }"
if errorlevel 1 exit /b 1
if exist "%RM_PID_FILE%" del /q "%RM_PID_FILE%" >nul 2>&1
exit /b 0
