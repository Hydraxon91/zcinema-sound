@echo off
setlocal EnableExtensions
title Z Cinema Sound
set "REPO=%~dp0"

:menu
cls
echo ==========================================================
echo    Z Cinema Sound   -   Logitech Z Cinema control panel
echo ==========================================================
echo.
echo    1) Open control panel (tray app)            (no admin)
echo    2) Install / update profile                 (admin)
echo    3) Calibrate volume ceiling                 (no admin)
echo    4) Re-enable profile                        (admin)
echo    5) Bypass - disable all processing          (admin)
echo    6) Uninstall                                (admin)
echo    7) Open Equalizer APO config folder
echo    8) Get Equalizer APO log                    (admin)
echo    9) Probe remote buttons                     (admin)
echo   10) Remote bridge (buttons to presets)        (no admin)
echo.
echo    0) Exit
echo.
set /p "sel=Select: "

if "%sel%"=="1" goto gui
if "%sel%"=="2" goto install
if "%sel%"=="3" goto calibrate
if "%sel%"=="4" goto enable
if "%sel%"=="5" goto bypass
if "%sel%"=="6" goto uninstall
if "%sel%"=="7" goto folder
if "%sel%"=="8" goto log
if "%sel%"=="9" goto probe
if "%sel%"=="10" goto bridge
if "%sel%"=="0" exit /b 0
goto menu

:gui
powershell -NoProfile -ExecutionPolicy Bypass -File "%REPO%tools\ZCinema-GUI.ps1"
goto menu

:install
powershell -NoProfile -Command "$q=[char]34; Start-Process powershell -Verb RunAs -ArgumentList @('-NoProfile','-ExecutionPolicy','Bypass','-NoExit','-File',($q+'%REPO%src\Install-ZCinema.ps1'+$q))"
goto menu

:calibrate
powershell -NoProfile -ExecutionPolicy Bypass -File "%REPO%tools\Calibrate-Ceiling.ps1"
echo.
pause
goto menu

:enable
powershell -NoProfile -Command "$q=[char]34; Start-Process powershell -Verb RunAs -ArgumentList @('-NoProfile','-ExecutionPolicy','Bypass','-NoExit','-File',($q+'%REPO%src\Bypass-ZCinema.ps1'+$q),'-Restore')"
goto menu

:bypass
powershell -NoProfile -Command "$q=[char]34; Start-Process powershell -Verb RunAs -ArgumentList @('-NoProfile','-ExecutionPolicy','Bypass','-NoExit','-File',($q+'%REPO%src\Bypass-ZCinema.ps1'+$q))"
goto menu

:uninstall
powershell -NoProfile -Command "$q=[char]34; Start-Process powershell -Verb RunAs -ArgumentList @('-NoProfile','-ExecutionPolicy','Bypass','-NoExit','-File',($q+'%REPO%src\Uninstall-ZCinema.ps1'+$q))"
goto menu

:folder
if exist "%ProgramFiles%\EqualizerAPO\config" (
    explorer "%ProgramFiles%\EqualizerAPO\config"
) else (
    echo Equalizer APO config folder was not found.
    pause
)
goto menu

:log
powershell -NoProfile -Command "$q=[char]34; Start-Process powershell -Verb RunAs -ArgumentList @('-NoProfile','-ExecutionPolicy','Bypass','-NoExit','-File',($q+'%REPO%tools\Get-EqualizerApoLog.ps1'+$q),'-Trace')"
goto menu

:probe
powershell -NoProfile -Command "$q=[char]34; Start-Process powershell -Verb RunAs -ArgumentList @('-NoProfile','-ExecutionPolicy','Bypass','-NoExit','-File',($q+'%REPO%tools\Remote-Probe.ps1'+$q))"
goto menu

:bridge
powershell -NoProfile -ExecutionPolicy Bypass -File "%REPO%tools\Remote-Bridge.ps1"
goto menu
