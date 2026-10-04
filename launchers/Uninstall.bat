@echo off
setlocal
set "REPO=%~dp0.."
powershell -NoProfile -Command "Start-Process powershell -Verb RunAs -ArgumentList '-NoProfile','-ExecutionPolicy','Bypass','-NoExit','-File','%REPO%\src\Uninstall-ZCinema.ps1'"
