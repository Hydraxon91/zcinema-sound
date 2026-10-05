# Anti-cheat and DRM notes

> **Note:** the PowerShell toolkit referenced below now lives under `legacy\`
> (see `legacy\README.md`); the native app is in `src-app\`.

Short version: **this project is designed to be safe for kernel-level anti-cheat
and DRM**, because it installs no kernel driver and does not enable test-signing.

## What ZCinema Sound does *not* do

- No kernel-mode driver (`*.sys`)
- No test-signing / `bcdedit /set testsigning on` / `nointegritychecks`
- No DLL injection into games
- No overlay, no hooking, no memory patching

## Why that matters

- **BattlEye** does not support systems running in **test-signing or kernel
  debugging** modes (per its FAQ), and treats non-standard driver environments
  as suspicious.
- **Riot Vanguard** checks Secure Boot, driver signatures and more, and loads
  before other drivers.

The *driver* approach (installing the original 2007 SRS package) requires
test-signing, which is exactly what these anti-cheats reject — so this project
avoids it entirely.

## What it does instead

It configures **Equalizer APO** (GPLv2, user-mode), an Audio Processing Object
loaded by `audiodg.exe`, the same place every legitimate sound-card effect runs.
This is the standard, widely-used approach among gamers for system-wide EQ.

## Residual risk (being honest)

No anti-cheat publishes a whitelist of approved APOs, so nothing can be
"guaranteed." Equalizer APO is widely used with games and anti-cheat and is not
a cheat tool. If a specific title ever objects:

1. Open Equalizer APO's **Configurator** and untick the device (or
   `tools\`/Configurator troubleshooting "use original APO" options), or
2. Run `src\Uninstall-ZCinema.ps1` and untick the device.

Your games will launch; the profile just stops applying.

## DRM

No DRM is affected: no protected path is modified, no display capture, no
audio-stream interception outside the normal Windows audio engine.
