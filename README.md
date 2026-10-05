# ZCinema Sound

<p align="center"><img src="assets/ZCinemaSound.png" width="112" alt="ZCinema Sound icon"></p>

Makes the **Logitech Z Cinéma** USB speakers sound right on Windows 10/11 — proper
volume, bass, treble and EQ, plus remote-button control — **with no kernel driver**.

**[⬇ Download the installer](https://github.com/Hydraxon91/zcinema-sound/releases)** (`ZCinemaSound-Setup.exe`)

## What is this?

The Z Cinéma is a ~2007 2.1 USB speaker set. Windows detects it fine, but two things
are broken today: the volume slider feels "maxed out" around 40%, and the original
Logitech/SRS tone controls (bass, treble, dialog clarity, surround) are gone because
the 2007 driver can't install on modern Windows.

ZCinema Sound is a small **tray app** that fixes both in user space. It drives
[Equalizer APO](https://sourceforge.net/projects/equalizerapo/) — the open-source
engine that actually processes the audio — and gives you working volume, Bass/Treble,
an 8-band EQ, stereo width and dialogue clarity, presets, per-device tuning and
remote-button mapping. It runs entirely in **user mode (no kernel driver, no
test-signing)**, so it's safe to use alongside games and kernel-level anti-cheat.

![ZCinema Sound — Sound tab](docs/images/screenshot.png)
![ZCinema Sound — Remote tab](docs/images/screenshot-remote.png)

## Features

| What you get | Details |
|---|---|
| **Volume + mute** | Drives the speakers' Windows volume, so the slider, keyboard keys, on-screen display and the remote all stay in sync. |
| **Ceiling calibration** | Measures the real volume taper and sets a ceiling so **100% = your comfortable maximum** (fixes the "maxed out at 40%" feel). |
| **Bass / Treble** | Broad tone controls. |
| **8-band EQ** | Parametric bands for finer shaping. |
| **Dialogue clarity** | A gentle boost around 3 kHz so voices cut through. |
| **Width** | Stereo widening — an approximation of the old surround staging. |
| **Presets** | Flat / Music / Movies / Night / Vocal / V-Shape, plus 3 savable Custom slots you can rename or clear. |
| **Per-device tuning** | Each output device keeps its own settings. |
| **Remote mapping** | Reassign the speaker remote's spare buttons to presets or actions (apps, scripts, URLs, key macros), with a **Learn** mode. |
| **Tray app** | Starts in the tray. Its menu has Presets, global hotkeys, bypass, backup/restore, update check and more. |

## Requirements

- **Windows 10 or 11, 64-bit.**
- [**Equalizer APO**](https://sourceforge.net/projects/equalizerapo/) installed (GPLv2).
  It does the audio processing and is **not bundled** with this project.
- The **Z Cinéma** connected.

## Quick start

1. **Download** `ZCinemaSound-Setup.exe` from the
   [latest release](https://github.com/Hydraxon91/zcinema-sound/releases) and run it.
2. **If Equalizer APO isn't installed**, setup opens its download page and waits —
   install it, then click **Retry**.
3. At the end, tick **Open Equalizer APO Device Selector** (or open it later from the
   tray menu), select **Speakers (Z Cinéma)**, and click OK.
4. **Reboot** so the effects attach.

That's it — the app starts in the tray. Open it from the tray icon, or press `Ctrl+Alt+0`.

> The app is **unsigned** for now, so Windows SmartScreen may warn on first run
> ("More info → Run anyway").

### Other install options

- **`ZCinemaSound-Setup-lite.exe`** — a smaller installer that needs the
  [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)
  (setup checks for it).
- **`ZCinemaSound.App.exe`** — a portable single-file build (no installer, no runtime
  needed on the target).
- **WinGet** — `winget install Hydraxon91.ZCinemaSound` (portable; once the package is
  published — see `winget/README.md`).
- **For all users vs. me only** — setup asks. "All users" installs to Program Files
  (admin); "me only" installs to `%LocalAppData%\Programs` (no admin).

## How volume works

Keep using **Windows' own volume** as your everyday control — the remote's volume keys
drive it too, so everything stays in sync.

ZCinema Sound doesn't replace that. It sets a **ceiling** (an overall trim, called
*Preamp*): if the speakers get loud too early, lower the ceiling so the whole slider
is usable. The app's **Calibrate ceiling** button measures the real volume taper and
tells you the exact value. Don't use a custom volume curve — it desyncs the Windows UI.

## Limitations

Straight about what this can't do.

**Audio processing**

- **No true surround.** The hardware is 2.1 (two satellites + a sub). Width is stereo
  widening, not real rear-channel simulation.
- **No room correction.** The EQ is manual — no measurement mic, no auto-tuning.
- **No exclusive-mode processing.** Equalizer APO is a shared-mode effect; apps that
  take the device in exclusive mode bypass it.
- **No original SRS processing.** The 2007 Logitech/SRS effects (TruSurround, TruBass,
  dialog clarity) can't be reproduced on the modern audio engine. <!-- TODO: verify -->

**Hardware and speaker controls**

- **No access to the speaker's own bass/treble.** Those live in the Z Cinéma's firmware
  (the controls under the volume on the remote); software can't read or move them. The
  Bass/Treble sliders here are a separate layer on top.
- **Nothing is stored on the device.** Settings are per-PC. On another PC, a console or
  Bluetooth, the sound is stock.

**Platform and compatibility**

- **No kernel driver, by design.** That's what keeps it anti-cheat-safe — but it also
  means no driver-level hooks or vendor control-panel integration.
- **Anti-cheat is never a guarantee.** Equalizer APO is user-mode and widely used with
  games, but no anti-cheat promises anything. You can disable processing any time with
  the tray's **Bypass processing**.

**Packaging**

- **Unsigned for now**, so SmartScreen may warn on first run. Code signing is staged in
  CI — see [`docs/SIGNING.md`](docs/SIGNING.md).

## FAQ & troubleshooting

Stuck? Start with [`docs/TROUBLESHOOTING.md`](docs/TROUBLESHOOTING.md). Related reading:

- Anti-cheat and DRM notes — [`docs/ANTI-CHEAT.md`](docs/ANTI-CHEAT.md)
- Remote button protocol — [`docs/REMOTE-CODES.md`](docs/REMOTE-CODES.md)
- Code signing — [`docs/SIGNING.md`](docs/SIGNING.md)

## Build from source

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/).

```powershell
cd src-app
dotnet build
dotnet run --project ZCinemaSound.App   # run it
dotnet test                             # unit tests
```

Single-file, self-contained `.exe` (no runtime needed on the target):

```powershell
dotnet publish ZCinemaSound.App -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

The app manages the Equalizer APO wiring itself (tray menu → **Install / update
profile**, **Bypass processing**, **Uninstall**; install and uninstall self-elevate).
Its profile lives at `…\EqualizerAPO\config\ZCinema.txt`, and your data (device
profiles, remote mappings, custom slots, settings, backups) lives in
`%APPDATA%\ZCinemaSound`.

<details>
<summary>Per-device EQ scoping (technical detail)</summary>

With two or more devices configured, the app writes Equalizer APO a scoped block per
device, matched by the device's **endpoint GUID** (the unique ID Windows gives each
output), with an `Else` fallback to the active device's profile — so every device keeps
its own sound and audio can never go silent. Prefer one global profile? Turn off
**Per-device EQ scoping** in the tray menu.
</details>

## Project layout

```
src-app/                  the app (C#/.NET 10)
  ZCinemaSound.Core/      profile model, presets, device store, HID decode, updater
  ZCinemaSound.App/       WinForms tray app (Sound + Remote tabs)
  ZCinemaSound.Tests/     xUnit tests
assets/                   icon artwork
installer/                Inno Setup script (zcinema.iss)
docs/                     ANTI-CHEAT, TROUBLESHOOTING, PRESETS, REMOTE-CODES, PORTING, ROADMAP, SIGNING
winget/                   WinGet package manifests
legacy/                   PowerShell/.bat toolkit (unsupported) — see legacy/README.md
```

## Legacy PowerShell toolkit (unsupported)

The native app above is **the product**. The original PowerShell toolkit is kept only
as an unmaintained reference/regression harness and lives under **`legacy\`**. It
shares the same profile and single-instance mutex as the app, so **don't run both at
once**; run it from inside `legacy\`. See [`legacy/README.md`](legacy/README.md).

<details>
<summary>Legacy install / control panel / uninstall</summary>

From `legacy\`:

- **Install:** `legacy\src\Install-ZCinema.ps1` (admin) copies
  `legacy\config\ZCinema.txt` into the Equalizer APO config folder and points
  `config.txt` at it (backs up the old one as `config.txt.bak-<timestamp>`).
- **Control panel:** `legacy\tools\ZCinema-GUI.ps1`.
- **Calibration:** `legacy\tools\Calibrate-Ceiling.ps1`.
- **Uninstall:** `legacy\src\Uninstall-ZCinema.ps1`.
</details>

## Credits / legal

- Audio engine: **Equalizer APO** by Jonas Thedering (GPLv2) — not included here.
- Not affiliated with, or endorsed by, Logitech or SRS Labs. "Logitech", "Z Cinéma"
  and "TruSurround"/"TruBass" are trademarks of their owners, used descriptively.
- This repo contains **no** Logitech/SRS binaries and requires none.
- Our code: MIT (see [`LICENSE`](LICENSE)).
- Icon palette and artwork notes: [`docs/DESIGN.md`](docs/DESIGN.md).
