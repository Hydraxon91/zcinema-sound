@echo off
setlocal
set "REPO=%~dp0.."
powershell -NoProfile -Command "Start-Process powershell -Verb RunAs -ArgumentList '-NoProfile','-ExecutionPolicy','Bypass','-NoExit','-File','%REPO%\tools\Remote-Probe.ps1','-Seconds','45'"
