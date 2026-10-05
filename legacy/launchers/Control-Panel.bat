@echo off
setlocal
set "REPO=%~dp0.."
powershell -NoProfile -ExecutionPolicy Bypass -File "%REPO%\tools\ZCinema-GUI.ps1"
