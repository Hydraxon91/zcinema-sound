@echo off
setlocal
set "REPO=%~dp0.."
powershell -NoProfile -ExecutionPolicy Bypass -File "%REPO%\tools\Calibrate-Ceiling.ps1"
echo.
pause
