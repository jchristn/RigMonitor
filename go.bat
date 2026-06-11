@echo off
setlocal

set "ROOT=%~dp0"
if "%ROOT:~-1%"=="\" set "ROOT=%ROOT:~0,-1%"
set "CONFIGURATION=%RIGMONITOR_CONFIGURATION%"
if "%CONFIGURATION%"=="" set "CONFIGURATION=Debug"
set "SERVER_PROJECT=%ROOT%\src\RigMonitor.Server\RigMonitor.Server.csproj"
set "RUN_DIR=%ROOT%\.rigmonitor-native"
set "SERVER_EXE=%RUN_DIR%\RigMonitor.Server.exe"
set "SERVER_DLL=%RUN_DIR%\RigMonitor.Server.dll"
set "SETTINGS_FILE=%ROOT%\rigmonitor.json"

echo RigMonitor native startup
echo Root: %ROOT%
echo Configuration: %CONFIGURATION%
echo.

pushd "%ROOT%\dashboard" || exit /b 1
if exist package-lock.json (
    call npm.cmd ci || exit /b 1
) else (
    call npm.cmd install || exit /b 1
)
call npm.cmd run build || exit /b 1
popd

if not exist "%ROOT%\data" mkdir "%ROOT%\data"
if not exist "%ROOT%\data\logs" mkdir "%ROOT%\data\logs"
if not exist "%RUN_DIR%" mkdir "%RUN_DIR%"

pushd "%ROOT%" || exit /b 1
dotnet build "%SERVER_PROJECT%" -c "%CONFIGURATION%" -o "%RUN_DIR%" || exit /b 1

echo.
echo Starting RigMonitor from "%ROOT%"
echo Runtime: "%RUN_DIR%"
echo Settings: "%SETTINGS_FILE%"
echo Dashboard: http://localhost:9990/dashboard
echo OpenAPI:   http://localhost:9990/openapi
echo Press Ctrl+C to stop.
echo.

if exist "%SERVER_EXE%" (
    "%SERVER_EXE%" --settings "%SETTINGS_FILE%"
) else (
    dotnet "%SERVER_DLL%" --settings "%SETTINGS_FILE%"
)
set "EXIT_CODE=%ERRORLEVEL%"
popd

exit /b %EXIT_CODE%
