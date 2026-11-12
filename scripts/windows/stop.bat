@echo off
rem Stop RigMonitor on Windows.
setlocal
call "%~dp0common.bat"

set "RM_PID="
if exist "%RM_PID_FILE%" set /p RM_PID=<"%RM_PID_FILE%"
if defined RM_PID (
    tasklist /FI "PID eq %RM_PID%" /FI "IMAGENAME eq RigMonitor.Server.exe" /NH 2>nul | find "%RM_PID%" >nul || set "RM_PID="
)

if defined RM_PID (
    echo [rigmonitor] Stopping RigMonitor ^(PID %RM_PID%^)
    taskkill /PID %RM_PID% /T /F >nul
    if exist "%RM_PID_FILE%" del /q "%RM_PID_FILE%"
    echo [rigmonitor] Stopped
    exit /b 0
)

rem Fall back to any RigMonitor.Server.exe started from this repo's runtime directory.
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command ^
  "$procs = @(Get-CimInstance Win32_Process | Where-Object { $_.Name -eq 'RigMonitor.Server.exe' -and $_.ExecutablePath -eq $env:RM_SERVER_EXE });" ^
  "if ($procs.Count -eq 0) { Write-Output '[rigmonitor] RigMonitor is not running'; exit 0 }" ^
  "foreach ($p in $procs) { Write-Output ('[rigmonitor] Stopping RigMonitor (PID ' + $p.ProcessId + ')'); Stop-Process -Id $p.ProcessId -Force }" ^
  "Write-Output '[rigmonitor] Stopped'"
if exist "%RM_PID_FILE%" del /q "%RM_PID_FILE%"
exit /b 0
