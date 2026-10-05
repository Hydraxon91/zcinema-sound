# Roadmap

Where this project is and where it's going. Nothing here changes the current,
working setup — it's the plan for packaging.

> **Direction:** the native **C#/.NET** app (`src-app\`) is now the product.
> The PowerShell toolkit is **legacy/reference** — still functional, kept as the
> regression harness until the `.exe` reaches full parity.
>
> **Status (v0.2.0):** shipped via GitHub Releases as `ZCinemaSound-Setup.exe`
> (self-contained), `ZCinemaSound-Setup-lite.exe` (needs the .NET Desktop Runtime)
> and portable `ZCinemaSound.App.exe`. Per-device profiles with **per-device EQ
> scoping**, a device selector, remote import/export, tray preset quick-switching
> and one-file backup/restore are done — see `CHANGELOG.md`.
>
> **Next (Batch 6):** DPI (`PerMonitorV2`) + layout scaling pass, first-run guidance,
> uninstall data cleanup, docs.

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

## Remote buttons (planned feature)

The remote already works inbox for media keys; the goal is to claim *extra*
buttons for our own actions. Feasibility is proven by `tools\Remote-Probe.ps1`
(user-mode HID sniffer; no driver). The Consumer (`COL01`) and Vendor
`FFBC:0088` (`COL02`) collections open without elevation; the Keyboard
collection (`COL03`) needs Administrator to read (anti-keylogger protection).

Per button, based on which collection it arrives on:

| Arrives as... | Mechanism | Conflict |
|---|---|---|
| Keyboard key (COL03) | PowerToys Keyboard Manager / AutoHotkey (user-mode) | none |
| Consumer usage the OS handles (COL01) | AutoHotkey if supported, else our HID reader | OS may also act |
| Free consumer usage, or the vendor collection (COL02) | our user-mode HID reader | none (best case) |

Deliverable: a background **remote bridge** (user-mode HID read + JSON mapping)
with a **Learn** mode in the GUI (press a button, assign an action). Intended
capabilities — all of them:

- **Presets + GUI:** Windows button -> open/focus the control panel; buttons to
  load Movies / Music / Night / Vocal / V-Shape presets; Custom 1/2/3.
- **Sound controls:** step the volume ceiling, mute, Dialogue, or Width.
- **General shortcuts:** launch/close apps, media transport, macros.

Constraints (deliberate):

- **User-mode only** — no kernel HID filter, no test-signing; anti-cheat stays
  unaffected.
- **No hijacking** of keys Windows already uses (volume/play). A few OS-handled
  buttons may double-fire; suppressing that needs a kernel tool (we won't).
- Reading the Keyboard collection requires Administrator; Consumer/Vendor do not.

Status: Phase 1 probe built (`tools\Remote-Probe.ps1`); protocol in
`docs\REMOTE-CODES.md`. Phase 2/3 built and **merged into the app**: the GUI is
now a **tray app** that hosts the remote bridge in-process (single-instance),
hides to tray on close, and offers **Start with Windows** (autostart launches it
with `-Tray`). Only keys Windows ignores are shown (native transport/volume/back
hidden); the **Media Player** button is exposed too since it does nothing on
Win11. Action library: `preset:`, `custom:`, `gui`, `bypass`,
`sound:` (dialogue/width/ceiling ±), `media:`, `app:`, `script:`, `url:`, `keys:`,
`none`. **Learn** captures any free button, and **Browse...** fills the Value from
a file picker. The standalone `tools\Remote-Bridge.ps1` remains for headless use
and stands down while the app is running (shared mutex).

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
