@echo off
rem Start RigMonitor on Windows in the background (hidden window).
rem Always builds first. A running instance is stopped before the build,
rem because Windows locks the files of a running RigMonitor.Server.exe.
setlocal
call "%~dp0common.bat" require-not-admin || exit /b 1

if not "%~1"=="" (
    echo Usage: %~nx0
    exit /b 2
)

where npm.cmd >nul 2>&1 || (echo [rigmonitor] ERROR: npm not found on PATH & exit /b 1)
where dotnet >nul 2>&1 || (echo [rigmonitor] ERROR: dotnet not found on PATH & exit /b 1)

rem Make sure the build and runtime folders are writable before touching anything.
for %%D in ("%RM_ROOT%" "%RM_ROOT%\dashboard" "%RM_ROOT%\dashboard\node_modules" "%RM_ROOT%\dashboard\dist" "%RM_RUN_DIR%" "%RM_ROOT%\data" "%RM_LOG_DIR%") do (
    if exist "%%~D\" (
        type nul > "%%~D\.rigmonitor-write-test" 2>nul || (call :not_writable "%%~D" & exit /b 1)
        del /q "%%~D\.rigmonitor-write-test" >nul 2>&1
    )
)
call "%~dp0stop.bat" || exit /b 1

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

if not exist "%RM_ROOT%\data" mkdir "%RM_ROOT%\data"
if not exist "%RM_LOG_DIR%" mkdir "%RM_LOG_DIR%"

powershell.exe -NoProfile -ExecutionPolicy Bypass -Command ^
  "$ErrorActionPreference='Stop';" ^
  "$p = Start-Process -FilePath $env:RM_SERVER_EXE -ArgumentList @('--settings', ('{0}{1}{0}' -f [char]34, $env:RM_SETTINGS_FILE)) -WorkingDirectory $env:RM_ROOT -WindowStyle Hidden -PassThru;" ^
  "try { Set-Content -Path $env:RM_PID_FILE -Value $p.Id -Encoding ascii } catch { Stop-Process -Id $p.Id -Force; Write-Output ('[rigmonitor] ERROR: could not write ' + $env:RM_PID_FILE + ': ' + $_.Exception.Message); exit 2 }" ^
  "Start-Sleep -Seconds 2;" ^
  "if ($p.HasExited) { Remove-Item -Force $env:RM_PID_FILE; exit 1 }"
if errorlevel 2 exit /b 1
if errorlevel 1 (
    echo [rigmonitor] ERROR: RigMonitor exited during startup; see logs in "%RM_LOG_DIR%"
    exit /b 1
)
set "RM_PID="
set /p RM_PID=<"%RM_PID_FILE%"
echo [rigmonitor] Started RigMonitor ^(PID %RM_PID%^)
echo [rigmonitor] Logs: "%RM_LOG_DIR%"
echo [rigmonitor] Dashboard: http://localhost:9990/dashboard
echo [rigmonitor] OpenAPI:   http://localhost:9990/openapi
exit /b 0

:not_writable
echo [rigmonitor] ERROR: %~1 is not writable by %USERNAME%.
echo [rigmonitor] This usually comes from an earlier run as administrator. From an elevated prompt, fix with:
echo [rigmonitor]   icacls "%RM_ROOT%" /grant "%USERNAME%":^(OI^)^(CI^)F /T
echo [rigmonitor]   attrib -R "%RM_ROOT%\*" /S /D
exit /b 1
