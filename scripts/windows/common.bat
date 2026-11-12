@echo off
rem Shared settings for the RigMonitor Windows scripts. Called, not run directly.
rem   call common.bat                    set variables
rem   call common.bat require-not-admin  also fail when running elevated
rem Environment overrides:
rem   RIGMONITOR_CONFIGURATION  dotnet build configuration (default: Release)
rem   RIGMONITOR_SETTINGS       settings file (default: <repo>\rigmonitor.json)
rem   RIGMONITOR_ALLOW_ADMIN=1  allow start/install from an elevated prompt

for %%I in ("%~dp0..\..") do set "RM_ROOT=%%~fI"
set "RM_CONFIGURATION=%RIGMONITOR_CONFIGURATION%"
if "%RM_CONFIGURATION%"=="" set "RM_CONFIGURATION=Release"
set "RM_SERVER_PROJECT=%RM_ROOT%\src\RigMonitor.Server\RigMonitor.Server.csproj"
set "RM_RUN_DIR=%RM_ROOT%\.rigmonitor-native"
set "RM_SERVER_EXE=%RM_RUN_DIR%\RigMonitor.Server.exe"
set "RM_SERVER_DLL=%RM_RUN_DIR%\RigMonitor.Server.dll"
set "RM_SETTINGS_FILE=%RIGMONITOR_SETTINGS%"
if "%RM_SETTINGS_FILE%"=="" set "RM_SETTINGS_FILE=%RM_ROOT%\rigmonitor.json"
set "RM_PID_FILE=%RM_RUN_DIR%\rigmonitor.pid"
set "RM_LOG_DIR=%RM_ROOT%\data\logs"
set "RM_SHORTCUT_NAME=RigMonitor.lnk"

if /i not "%~1"=="require-not-admin" exit /b 0
if "%RIGMONITOR_ALLOW_ADMIN%"=="1" exit /b 0
net session >nul 2>&1
if errorlevel 1 exit /b 0
echo [rigmonitor] ERROR: do not run this from an elevated (administrator) prompt.
echo [rigmonitor] Files built as administrator can be unwritable for your normal account, and
echo [rigmonitor] an elevated RigMonitor can only be stopped from an elevated prompt.
echo [rigmonitor] Re-run from a normal prompt, or set RIGMONITOR_ALLOW_ADMIN=1 to override.
exit /b 1
