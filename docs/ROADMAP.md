# Roadmap

Where this project is and where it's going. Nothing here changes the current,
working setup — it's the plan for packaging.

## 0. Done

- Equalizer APO profile + installer, GUI with sliders/presets, calibration,
  bypass, uninstall.
- `.bat` launchers (`ZCinema.bat` menu + `launchers\*.bat`).

## 1. `.bat` launchers — done

`ZCinema.bat` is a small menu that runs the PowerShell scripts (self-elevating
for the actions that need admin). Individual launchers live in `launchers\` for
desktop shortcuts.

## 2. A downloadable `.exe` (fast path) — next

Wrap the existing GUI into a single `.exe` with **ps2exe** so users don't need
to clone the repo:

- `tools\Build-Exe.ps1` that:
  1. inlines `ZCinema.Common.psm1` + `ZCinema-GUI.ps1` into one script,
  2. calls ps2exe with an icon, product/version info, and no console window,
  3. writes `dist\ZCinemaSound.exe`.
- A GitHub Actions workflow (windows-latest) runs it on a version tag and
  attaches the `.exe` to the Release.

Pros: reuses tested code, ships today. Cons: script-in-exe can trip some AV,
starts slower, still uses the in-box PowerShell runtime.

## 3. The real `.exe` (all-in-one) — later

A native **C#/.NET** app that is the whole product: install, uninstall,
calibrate, bypass and the control panel in one window/CLI. Unsigned for now;
code-signing is a later add-on.

```
src-app/
  ZCinemaSound.sln
  ZCinemaSound.Core/          profile model, presets, registry lookup  (+ xUnit tests)
  ZCinemaSound.App/           WinForms/WPF: sliders, graphic EQ, presets, tray icon
  ZCinemaSound.Cli/           optional verbs: install | uninstall | calibrate | bypass | gui
```

Key design points:

- **Locate Equalizer APO via the registry** (`HKLM\SOFTWARE\EqualizerAPO`,
  `ConfigPath`) instead of hard-coded paths.
- **Profile model** ports `Get-ZCinemaProfileParams` / `New-ZCinemaProfileText`;
  writes ASCII/no-BOM (Equalizer APO is picky about encoding).
- **Presets** in `%APPDATA%\ZCinemaSound\presets.json` (same format as now).
- **Elevation**: keep the "grant the user write access to the profile once at
  install" approach so the app itself runs unelevated; only Install/Uninstall
  request admin.
- **Per-device profiles** and a **tray icon** (quick preset switching) are easy
  wins once it's a real app.
- **Packaging**: `dotnet publish -c Release -r win-x64 --self-contained true
  -p:PublishSingleFile=true` → one `.exe`, no runtime install needed.

## 4. CI/CD

- GitHub Actions on `windows-latest`:
  - stage 2: build the ps2exe artifact;
  - stage 3: `dotnet build` + `dotnet test` + publish self-contained, attach to
    Releases.
- Keep the PowerShell scripts as the reference implementation and run their
  logic tests where practical; port them to `ZCinemaSound.Core` for proper unit
  tests later.

## 5. Code signing (deferred)

The `.exe` is **unsigned** for now, so Windows SmartScreen may warn on first
run ("unknown publisher → Run anyway"). If we ever get an Authenticode
certificate, signing removes that warning; the build pipeline already produces
the artifact to sign. No kernel code anywhere, so no driver signing is involved
(and gaming/anti-cheat stays unaffected).

## Invariants to preserve

- No kernel drivers, no test-signing, no anti-cheat impact.
- Positioned files only: everything is a value in an Equalizer APO config.
- Bundles no proprietary Logitech/SRS binaries.
