@echo off
setlocal
set "REPO=%~dp0.."
powershell -NoProfile -Command "$q=[char]34; Start-Process powershell -Verb RunAs -ArgumentList @('-NoProfile','-ExecutionPolicy','Bypass','-NoExit','-File',($q+'%REPO%\src\Install-ZCinema.ps1'+$q))"
