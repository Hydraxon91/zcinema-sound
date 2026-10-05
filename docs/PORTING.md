# Porting to the native app — parity spec

The C# rewrite should be a **port** of the tested PowerShell behaviour, not a
fresh design. Everything here was learned the hard way; preserve it.

## Equalizer APO profile

- **Locate the engine:** `HKLM\SOFTWARE\EqualizerAPO` → `ConfigPath` (default
  `C:\Program Files\EqualizerAPO\config`). Profile file: `ZCinema.txt`; the engine
  loads it via `Include: ZCinema.txt` in `config.txt`.
- **Write as ASCII, no BOM, CRLF.** Equalizer APO is picky; a UTF-8 BOM + em dashes
  caused problems.
- **Line grammar**
  - `Preamp: <dB> dB` — the volume ceiling. **Multiple `Preamp:` lines SUM**
    (Equalizer APO ≥ 0.8), so keep exactly one across the active config.
  - `Filter: ON PK Fc <Hz> Hz Gain <dB> dB Q <q>` — use **PK** for bass/treble:
    this build **ignored `LSC`/`HSC` shelves**.
  - `Copy: L=L+-0.10*R` — negative crossfeed for stereo width. Use `+-0.10`
    (bare `-` fails to parse and **muted all audio**).
- **Editable fields** (generator/parser): `PreampDb`, `BassGain` (PK 100 Hz),
  `SubGain` (PK 45 Hz), `TrebleGain` (PK 8000 Hz), `DialogGain` (PK 3000 Hz),
  `Width` (crossfeed 0..0.30), `EqGains[8]` at 60 / 170 / 470 / 1200 / 2400 /
  4700 / 10000 / 14000 Hz.
- **Ceiling range must extend well below -20 dB** (e.g. to **-60 dB**) for very
  loud setups. The slider minimum, the `sound:ceiling-` clamp and the parser must
  all allow it — don't hard-cap at -20.

## `config.txt` management

- **Clean mode (default):** replace `config.txt` with `Include: ZCinema.txt`. The
  stock Equalizer APO config adds its own `Preamp:` and a demo bass boost, which
  would otherwise **sum** with ours.
- **Merge mode:** keep existing lines, **comment out existing `Preamp:` lines**,
  append the include.
- Always back up to `config.txt.bak-<timestamp>`; uninstall restores the newest
  pre-install backup.
- **Attach detection:** Equalizer APO 1.4+ records attachment under
  `HKLM\SOFTWARE\EqualizerAPO\Child APOs\<endpoint-guid>` — *not* in the
  endpoint's `FxProperties`.

## Volume model

- Keep **Windows volume native**; `Preamp` is the **ceiling**. No custom curve.
- Calibration: read master scalar↔dB (taper ≈ `dB = 33·log10(scalar)`); the
  preamp to make 100 % = your old comfortable % is `dB(scalar at that %)`.
- Core Audio interop IIDs used today: `IMMDeviceEnumerator`
  `A95664D2-9614-4F35-A746-DE8DB63617E6`, `IMMDevice`
  `D666063F-1587-4E43-81F1-B948E807363F`, `IAudioEndpointVolume`
  `5CDF2C82-841E-4546-9722-0CF74078229A`.

## Remote (HID)

- **Find collections:** `HKLM\SYSTEM\CurrentControlSet\Control\DeviceClasses\{4d1e55b2-f16f-11cf-88cb-001111000030}`
  subkeys containing `VID_046D&PID_0A0F`; skip `Col03` (keyboard). Open with
  `CreateFileW`, read with `ReadFile`.
- **Decode** per `docs\REMOTE-CODES.md`: map both report forms to the same button
  name and **de-duplicate within ~700 ms** so one press = one action.
- **UI exposes free keys only** (the Media Center leftovers, incl. the Media
  Player button); Windows-handled keys are hidden (would double-fire).
- **Action grammar** (stored as `"verb[:value]"`):
  `none` · `gui` · `bypass` · `preset:<Flat|Music|Movies|Night|Vocal|V-Shape>` ·
  `custom:<1|2|3>` · `sound:<dialogue+|dialogue-|width+|width-|ceiling+|ceiling->` ·
  `media:<playpause|next|prev|stop|volup|voldown|mute>` ·
  `app:<path>` · `script:<ps1>` · `url:<url>` · `keys:<SendKeys>`.
- `bypass` = swap the profile for a no-op and back (no admin), using
  `last-profile.txt` + `bypassed.flag`.
- `gui` = signal the running app to show (named event), else launch it.
- **Mapping file:** `%APPDATA%\ZCinemaSound\remote.json` = `{ "Button": "action[:value]" }`.
- **Custom presets:** `%APPDATA%\ZCinemaSound\presets.json` =
  `{ "1": {"Preamp","Bass","Treble","Dialog","Width","Eq":[8]} , "2": …, "3": … }`.

## Process / lifetime

- **Single instance:** mutexes `Local\ZCinema_App` (GUI) and `Local\ZCinema_Bridge`
  (headless bridge) — cross-check so only one reader ever runs.
- **Show event:** `Local\ZCinema_Show` (AutoReset) — a second launch signals it
  and exits; the running app shows its window.
- **Tray:** closing hides to tray; **Exit** quits; tray menu = *Open*, *Remote
  mapping on/off*, *Start with Windows*, *Exit*.
- **Autostart:** `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\ZCinemaSound` =
  `powershell -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "<gui>" -Tray`.

## Invariants (do not break)

- **No kernel driver, no test-signing.** Never write the endpoint `FxProperties`
  (SDL grants write only to `Audiosrv`/`AudioEndpointBuilder`/`TrustedInstaller`).
  This is what keeps it anti-cheat-safe.
- **Volume stays native**; no per-app injection.
- **Bundle no Logitech/SRS binaries** — Equalizer APO (GPLv2) is the engine and is
  installed by the user.
- Windows-handled keys stay hidden; free keys stay user-mappable.

## Packaging (.NET)

- WinForms/WPF, `dotnet publish -r win-x64 --self-contained -p:PublishSingleFile=true`.
- Unsigned for now (SmartScreen prompt accepted).
- Run **unelevated**: the installer grants the user write access to `ZCinema.txt`
  once; only install/uninstall elevate.
- App/tray icon: `assets\ZCinemaSound.ico` if present, else a system icon.

## Pitfalls already solved (don't reintroduce)

- `Start-Process -ArgumentList` does **not** quote elements — embedded-quote any
  path with spaces (or pass one quoted string).
- Equalizer APO configs must be **ASCII, no BOM**.
- Crossfeed must be `L=L+-0.10*R` (bare `-` broke parsing → silence).
- `LSC`/`HSC` shelves are ignored by this build → use PK bands.
- `Preamp:` lines **sum**.
- Audio endpoint registry keys are TrustedInstaller-only — don't fight them.
- PowerShell variable names are case-insensitive (`$tray` vs `$Tray` collision).

## Regression checklist

- [ ] profile written ASCII/no-BOM; Equalizer APO hot-reloads; sound changes.
- [ ] `config.txt` clean include; uninstall restores the backup.
- [ ] volume native; `Preamp` ceiling applies; calibration matched.
- [ ] each free remote key decodes once (deduped); mappings apply live; Learn.
- [ ] single instance; close-to-tray; Exit; autostart add/remove.
- [ ] standalone bridge stands down while the app holds `Local\ZCinema_App`.
