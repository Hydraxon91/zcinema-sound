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
echo    1) Open control panel (GUI)                 (no admin)
echo    2) Install / update profile                 (admin)
echo    3) Calibrate volume ceiling                 (no admin)
echo    4) Re-enable profile                        (admin)
echo    5) Bypass - disable all processing          (admin)
echo    6) Uninstall                                (admin)
echo    7) Open Equalizer APO config folder
echo    8) Get Equalizer APO log                    (admin)
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
if "%sel%"=="0" exit /b 0
goto menu

:gui
powershell -NoProfile -ExecutionPolicy Bypass -File "%REPO%tools\ZCinema-GUI.ps1"
goto menu

:install
powershell -NoProfile -Command "Start-Process powershell -Verb RunAs -ArgumentList '-NoProfile','-ExecutionPolicy','Bypass','-NoExit','-File','%REPO%src\Install-ZCinema.ps1'"
goto menu

:calibrate
powershell -NoProfile -ExecutionPolicy Bypass -File "%REPO%tools\Calibrate-Ceiling.ps1"
echo.
pause
goto menu

:enable
powershell -NoProfile -Command "Start-Process powershell -Verb RunAs -ArgumentList '-NoProfile','-ExecutionPolicy','Bypass','-NoExit','-File','%REPO%src\Bypass-ZCinema.ps1','-Restore'"
goto menu

:bypass
powershell -NoProfile -Command "Start-Process powershell -Verb RunAs -ArgumentList '-NoProfile','-ExecutionPolicy','Bypass','-NoExit','-File','%REPO%src\Bypass-ZCinema.ps1'"
goto menu

:uninstall
powershell -NoProfile -Command "Start-Process powershell -Verb RunAs -ArgumentList '-NoProfile','-ExecutionPolicy','Bypass','-NoExit','-File','%REPO%src\Uninstall-ZCinema.ps1'"
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
powershell -NoProfile -Command "Start-Process powershell -Verb RunAs -ArgumentList '-NoProfile','-ExecutionPolicy','Bypass','-NoExit','-File','%REPO%tools\Get-EqualizerApoLog.ps1','-Trace'"
goto menu
