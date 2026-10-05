# ZCinema Sound

<p align="center"><img src="assets/ZCinemaSound.png" width="112" alt="ZCinema Sound icon"></p>

> **Current app:** the native **C#/.NET** build in `src-app\` is the primary
> product — a themed tray app with the 8-band EQ, a Windows volume slider,
> ceiling calibration and remote-button mapping. The **PowerShell scripts below
> are legacy/reference** (they still work) and are kept until the `.exe` reaches
> full parity. See [Native app (C#)](#native-app-c).

## Native app (C#)

The current app lives in `src-app\` (`ZCinemaSound.Core` + `ZCinemaSound.App` +
`ZCinemaSound.Tests`). It needs the [.NET 10 SDK](https://dotnet.microsoft.com/) to build.

![ZCinema Sound app](docs/images/screenshot.png)

### Install (recommended)

Download **`ZCinemaSound-Setup.exe`** from the
[latest release](https://github.com/Hydraxon91/zcinema-sound/releases).

- **Equalizer APO is required** (GPLv2 — not bundled). The installer detects it
  and, if missing, opens its download page and waits for you to install it, then
  click **Retry** (or Cancel to abort).
- After setup, open **Equalizer APO's Device Selector**, tick
  **Speakers (Z Cinéma)**, click OK, then **reboot** so the effects attach.
- The installer is **unsigned**, so SmartScreen may warn ("More info → Run anyway").
- The same release also ships the portable **`ZCinemaSound.App.exe`** (no installer).
- **Smaller download?** **`ZCinemaSound-Setup-lite.exe`** is framework-dependent and
  needs the [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)
  (the installer checks for it). Use the regular `ZCinemaSound-Setup.exe` if you'd
  rather not install the runtime.

### Build from source

```powershell
cd src-app
dotnet build
dotnet run --project ZCinemaSound.App      # run it
dotnet test                                # unit tests
```

Single-file, self-contained `.exe` (no runtime needed on the target):

```powershell
dotnet publish ZCinemaSound.App -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

The tray menu has **Install / update profile**, **Bypass processing** and
**Uninstall** — so the app manages the Equalizer APO wiring itself (no legacy
scripts needed; install/uninstall self-elevate).

It shares the same profile (`…\EqualizerAPO\config\ZCinema.txt`) and the same
`%APPDATA%\ZCinemaSound\` data as the legacy scripts, and uses the same
single-instance mutex — so **don't run both at once**.

Sound profile + setup helper that makes the **Logitech Z Cinéma** USB speakers
sound right on modern Windows (10/11) — proper volume behaviour, bass, treble
and EQ — **without any kernel driver and without test-signing**.

It is a thin wrapper around [Equalizer APO](https://sourceforge.net/projects/equalizerapo/)
(GPLv2), which does the actual audio processing.

## Why this exists

The Z Cinéma is a ~2007 2.1 USB speaker set (`USB\VID_046D&PID_0A0F`). Windows
has always detected it fine (inbox `usbaudio`), but:

- the volume slider feels "maxed out" around 40 %, and
- the original Logitech/SRS tone controls (TruBass, TruSurround HD, bass/treble,
  dialog clarity) no longer exist, because the 2007 driver can't install on
  modern Windows.

This project fixes both in user space. No driver, no signing, safe for
kernel-level anti-cheat (EAC/BattlEye/Vanguard).

## What you get

| Feature | How |
|---|---|
| Volume that stays in sync | Windows' own volume everywhere (remote, OSD, media keys, per-app). `Preamp` sets the ceiling. |
| Bass / Treble | Broad peaking bands, used like a bass/treble control (a shelf is not used because this Equalizer APO build ignores `LSC`/`HSC`) |
| EQ | Parametric bands (edit text or use the Peace GUI) |
| TruSurround-ish width | Stereo crossfeed (`Copy:` lines) |
| Dialogue clarity | ~3 kHz presence boost |
| Live GUI | `tools\ZCinema-GUI.ps1` — **Sound** tab (Bass, Treble, Dialogue, Width, ceiling + 8-band EQ) and **Remote** tab (map remote buttons) |
| Tray app | The GUI **hosts the remote bridge** and lives in the notification tray: closing hides to tray, double-click/Open restores it, **Exit** quits. Optional **Start with Windows** (tray menu). |
| Presets | Flat / Music / Movies / Night / Vocal / V-Shape (with EQ), plus 3 savable Custom slots |

## What you don't get

Straight about the limits:

- **No real SRS TruSurround HD.** The stereo widener here is an approximation of
  the surround staging, not the patented algorithm. (The 2007 SRS APO DLL does
  load on Win11, but it can't be attached to the audio endpoint without a signed
  driver package — and even then it's unproven on the modern engine.)
- **No SRS treble / definition controls.** The original SRS software exposed
  extra treble-style controls (Definition, Dialog Clarity). Here treble is a
  single wide peaking band — similar in effect, but not the SRS processing.
- **No access to the speaker's own bass and treble.** The Z Cinéma's built-in
  bass/treble — the controls you reach under the volume (on the remote, or in the
  original Logitech panel) — live in the speaker's firmware. Software cannot read
  or move them. The GUI's Bass/Treble sliders are a separate layer added on top.
- **No true surround.** The hardware is 2.1 (two satellites + sub). There is no
  rear-channel simulation or multichannel decode — only stereo widening.
- **No ASIO / WASAPI-exclusive processing.** Equalizer APO is a shared-mode
  system effect; apps that take the device in exclusive mode bypass it.
- **No room correction.** The EQ is manual — no measurement mic, no auto-tuning.
- **Not stored on the device.** Settings are per-PC (an Equalizer APO config).
  On another PC, a console, or Bluetooth, the sound is stock.
- **No kernel driver, by design.** Good for anti-cheat, but it also means no
  driver-level hooks or vendor control-panel integration; nothing is test-signed.
- **Remote extras aren't implemented yet.** Media keys work inbox; the vendor HID
  collection (`FFBC:0088`) is unused so far. A user-mode remote bridge is planned
  — see `tools\Remote-Probe.ps1` and `docs\ROADMAP.md`.
- **Not a signed app.** It ships as an unsigned single-file `.exe` (once
  packaged) or PowerShell + Equalizer APO today; SmartScreen may warn on first run.
- **Anti-cheat caveat.** Equalizer APO is user-mode and widely used with games,
  but no anti-cheat guarantees anything — and you can disable it any time with
  `src\Bypass-ZCinema.ps1`.

## Requirements

- Windows 10/11, 64-bit
- [Equalizer APO](https://sourceforge.net/projects/equalizerapo/) installed
  (you install it yourself — it is **not** bundled here, to respect its GPL and
  to keep this repo free of third-party binaries)
- The Z Cinéma connected

## Easiest start: double-click `ZCinema.bat`

The repo root has a small menu that runs everything (self-elevating where
needed):

```
 1) Open control panel (tray app)
 2) Install / update profile        (admin)
 3) Calibrate volume ceiling
 4) Re-enable profile               (admin)
 5) Bypass - disable all processing (admin)
 6) Uninstall                       (admin)
 7) Open Equalizer APO config folder
 8) Get Equalizer APO log           (admin)
 9) Probe remote buttons            (admin)
10) Remote bridge (buttons to presets)
```

`launchers\` has the same actions as individual `.bat` files, handy for desktop
shortcuts. A proper one-file `.exe` is planned — see `docs\ROADMAP.md`.

The control panel (option 1) is a **tray app**: it also runs the remote bridge,
so your mapped remote buttons work while it's open. Closing the window keeps it
in the tray; use **Exit** (tray menu or the Remote tab) to quit, and
**Start with Windows** in the tray menu to keep mappings active at login.

## Install

1. Install Equalizer APO, run its **DeviceSelector** (older builds: Configurator), tick
   **Speakers (Z Cinéma)**, then reboot.
2. In an **Administrator** PowerShell, from the repo folder:

   ```powershell
   powershell -ExecutionPolicy Bypass -File .\src\Install-ZCinema.ps1
   ```

   It detects your Z Cinéma endpoint, copies `config\ZCinema.txt` into the
   Equalizer APO config folder, and points `config.txt` at it.

   Your previous `config.txt` is backed up beside it as
   `config.txt.bak-<timestamp>`. By default `config.txt` is replaced with a
   clean include (the Equalizer APO default config adds its own `Preamp:` and a
   demo bass boost, which would otherwise **sum** with this profile). Add
   `-Merge` to keep your existing config instead — existing `Preamp:` lines are
   neutralized because Equalizer APO adds them together.

3. Optional — set the volume ceiling by measurement instead of by ear:

   ```powershell
   powershell -ExecutionPolicy Bypass -File .\tools\Calibrate-Ceiling.ps1
   ```

4. Tune live with the GUI (no admin needed after step 2):

   ```powershell
   powershell -ExecutionPolicy Bypass -File .\tools\ZCinema-GUI.ps1
   ```

   Sliders for **Bass**, **Treble**, **Dialogue**, **Width** and the volume
   **Ceiling**, plus an **8-band graphic EQ** and Music/Movies/Night presets.
   Equalizer APO hot-reloads the profile, so changes are heard immediately.
   Prefer text? Edit `C:\Program Files\EqualizerAPO\config\ZCinema.txt`, or use
   the [Peace GUI](https://sourceforge.net/projects/peace-equalizer-apo-extension/).

## Volume, explained

Do **not** use a custom volume curve — it desyncs the Windows UI. Instead:

- Leave Windows volume as your everyday control. The Z Cinéma's remote volume
  keys drive it natively, so everything stays in sync.
- `Preamp:` in the config lowers the overall level so that **100 % equals your
  comfortable maximum**. If the speakers "max out" at ~40 %, a preamp around
  −8 to −13 dB makes the whole slider usable again.

`Calibrate-Ceiling.ps1` measures the endpoint's real dB taper and tells you the
exact preamp to paste in.

## Uninstall

```powershell
powershell -ExecutionPolicy Bypass -File .\src\Uninstall-ZCinema.ps1
```

Removes the include line and the profile (keeps a backup of `config.txt`).

## Layout

```
ZCinema.bat                       menu launcher (double-click)
launchers/                        per-action .bat files (for shortcuts)
assets/    ZCinemaSound.ico/.png  app + README artwork (8-size icon pack)
config/    ZCinema.txt            main profile (preamp ceiling + bass/treble/EQ)
presets/   *-addon.txt            optional extra filters (music / movies / night)
src/       Install / Uninstall    setup scripts
src/       Bypass-ZCinema.ps1     panic button: disable processing, restore audio
src/lib/   ZCinema.Common.psm1    shared helpers (profile parse/generate)
src/lib/   ZCinema.Remote.psm1    remote HID reader + action library
tools/     ZCinema-GUI.ps1        tray app: Sound + Remote tabs (hosts the bridge)
tools/     Remote-Probe.ps1       remote HID sniffer/decoder
tools/     Remote-Bridge.ps1      headless remote bridge (stands down if app runs)
tools/     Calibrate-Ceiling.ps1  measures the volume taper -> suggests Preamp
tools/     Get-EqualizerApoLog.ps1 fetch Equalizer APO's log (admin)
tools/lib/ ZCinemaAudio.cs        Core Audio interop used by the calibration tool
docs/      ANTI-CHEAT, TROUBLESHOOTING, PRESETS, REMOTE-CODES, PORTING, ROADMAP
```

## Icon

<img src="assets/ZCinemaSound.png" width="48" alt="icon">

Palette: `#010001` (major), `#D9872C` / `#AE4906` (accents), `#E1E0E1` (edge),
`#FAFBCA` (very minor). `assets\ZCinemaSound.ico` is an 8-size pack
(16/24/32/48/64/96/128/256) used for the window and tray icon, with a system-icon
fallback if it's missing.

## Credits / legal

- Audio engine: **Equalizer APO** by Jonas Thedering (GPLv2) — not included here.
- Not affiliated with, or endorsed by, Logitech or SRS Labs. "Logitech",
  "Z Cinéma" and "TruSurround"/"TruBass" are trademarks of their owners and are
  used descriptively.
- This repo contains **no** Logitech/SRS binaries and requires none.
- Our code: MIT (see `LICENSE`).
