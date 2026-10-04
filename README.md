# ZCinema Sound

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
| Bass / Treble | Low/high shelf filters |
| EQ | Parametric bands (edit text or use the Peace GUI) |
| TruSurround-ish width | Stereo crossfeed (`Copy:` lines) |
| Dialogue clarity | ~3 kHz presence boost |
| Live GUI | `tools\ZCinema-GUI.ps1` — Bass, Treble, Dialogue, Width, ceiling + 8-band EQ sliders |
| Presets | Flat / Music / Movies / Night / Vocal / V-Shape (with EQ), plus 3 savable Custom slots |

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
1) Open control panel (GUI)
2) Install / update profile        (admin)
3) Calibrate volume ceiling
4) Re-enable profile               (admin)
5) Bypass - disable all processing (admin)
6) Uninstall                       (admin)
7) Open Equalizer APO config folder
8) Get Equalizer APO log           (admin)
```

`launchers\` has the same actions as individual `.bat` files, handy for desktop
shortcuts. A proper one-file `.exe` is planned — see `docs\ROADMAP.md`.

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
config/    ZCinema.txt            main profile (preamp ceiling + bass/treble/EQ)
presets/   *-addon.txt            optional extra filters (music / movies / night)
src/       Install / Uninstall    setup scripts
src/       Bypass-ZCinema.ps1     panic button: disable processing, restore audio
src/lib/   ZCinema.Common.psm1    shared helpers (profile parse/generate)
tools/     ZCinema-GUI.ps1        sliders + graphic EQ for the profile
tools/     Calibrate-Ceiling.ps1  measures the volume taper -> suggests Preamp
tools/     Get-EqualizerApoLog.ps1 fetch Equalizer APO's log (admin)
tools/lib/ ZCinemaAudio.cs        Core Audio interop used by the calibration tool
docs/      ANTI-CHEAT, TROUBLESHOOTING, PRESETS, ROADMAP
```

## Credits / legal

- Audio engine: **Equalizer APO** by Jonas Thedering (GPLv2) — not included here.
- Not affiliated with, or endorsed by, Logitech or SRS Labs. "Logitech",
  "Z Cinéma" and "TruSurround"/"TruBass" are trademarks of their owners and are
  used descriptively.
- This repo contains **no** Logitech/SRS binaries and requires none.
- Our code: MIT (see `LICENSE`).
