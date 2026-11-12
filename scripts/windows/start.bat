@echo off
rem Start RigMonitor on Windows in the background (hidden window).
rem Builds on first run. Options: --rebuild  --build-only
setlocal
call "%~dp0common.bat"

set "REBUILD=0"
set "BUILD_ONLY=0"
:parse
if "%~1"=="" goto parsed
if /i "%~1"=="--rebuild" (set "REBUILD=1" & shift & goto parse)
if /i "%~1"=="--build-only" (set "BUILD_ONLY=1" & shift & goto parse)
echo Usage: %~nx0 [--rebuild] [--build-only]
exit /b 2
:parsed

if "%REBUILD%"=="1" goto build
if not exist "%RM_SERVER_EXE%" goto build
goto built

:build
where npm.cmd >nul 2>&1 || (echo [rigmonitor] ERROR: npm not found on PATH & exit /b 1)
where dotnet >nul 2>&1 || (echo [rigmonitor] ERROR: dotnet not found on PATH & exit /b 1)
echo [rigmonitor] Building dashboard
pushd "%RM_ROOT%\dashboard" || exit /b 1
if exist package-lock.json (
    call npm.cmd ci || (popd & exit /b 1)
) else (
    call npm.cmd install || (popd & exit /b 1)
)
call npm.cmd run build || (popd & exit /b 1)
popd
echo [rigmonitor] Building server (%RM_CONFIGURATION%)
if not exist "%RM_RUN_DIR%" mkdir "%RM_RUN_DIR%"
dotnet build "%RM_SERVER_PROJECT%" -c "%RM_CONFIGURATION%" -o "%RM_RUN_DIR%" || exit /b 1

:built
if "%BUILD_ONLY%"=="1" (echo [rigmonitor] Build complete & exit /b 0)

call :running_pid
if defined RM_PID (
    echo [rigmonitor] RigMonitor is already running ^(PID %RM_PID%^)
    goto urls
)

if not exist "%RM_ROOT%\data" mkdir "%RM_ROOT%\data"
if not exist "%RM_LOG_DIR%" mkdir "%RM_LOG_DIR%"

powershell.exe -NoProfile -ExecutionPolicy Bypass -Command ^
  "$ErrorActionPreference='Stop';" ^
  "$p = Start-Process -FilePath $env:RM_SERVER_EXE -ArgumentList @('--settings', ('{0}{1}{0}' -f [char]34, $env:RM_SETTINGS_FILE)) -WorkingDirectory $env:RM_ROOT -WindowStyle Hidden -PassThru;" ^
  "Set-Content -Path $env:RM_PID_FILE -Value $p.Id -Encoding ascii;" ^
  "Start-Sleep -Seconds 2;" ^
  "if ($p.HasExited) { Remove-Item -Force $env:RM_PID_FILE; exit 1 }"
if errorlevel 1 (
    echo [rigmonitor] ERROR: RigMonitor exited during startup; see logs in "%RM_LOG_DIR%"
    exit /b 1
)
call :running_pid
echo [rigmonitor] Started RigMonitor ^(PID %RM_PID%^)
echo [rigmonitor] Logs: "%RM_LOG_DIR%"

:urls
echo [rigmonitor] Dashboard: http://localhost:9990/dashboard
echo [rigmonitor] OpenAPI:   http://localhost:9990/openapi
exit /b 0

:running_pid
set "RM_PID="
if not exist "%RM_PID_FILE%" exit /b 0
set /p RM_PID=<"%RM_PID_FILE%"
if not defined RM_PID exit /b 0
tasklist /FI "PID eq %RM_PID%" /FI "IMAGENAME eq RigMonitor.Server.exe" /NH 2>nul | find "%RM_PID%" >nul || set "RM_PID="
exit /b 0
