@echo off
setlocal

set "ROOT=%~dp0"
if "%ROOT:~-1%"=="\" set "ROOT=%ROOT:~0,-1%"

echo Cleaning RigMonitor runtime files from "%ROOT%"

if exist "%ROOT%\rigmonitor.json" (
    del /f /q "%ROOT%\rigmonitor.json"
)

for /r "%ROOT%" %%F in (*.db *.db-* *.sqlite *.sqlite-* *.sqlite3 *.sqlite3-* *.log) do (
    del /f /q "%%F"
)

if exist "%ROOT%\logs" (
    rmdir /s /q "%ROOT%\logs"
)

if exist "%ROOT%\log" (
    rmdir /s /q "%ROOT%\log"
)

for /d /r "%ROOT%" %%D in (logs log) do (
    if exist "%%D" rmdir /s /q "%%D"
)

echo RigMonitor runtime cleanup complete.
