@echo off
setlocal
set "REPO=%~dp0.."
powershell -NoProfile -ExecutionPolicy Bypass -File "%REPO%\tools\Remote-Bridge.ps1"
