# Changelog

## v0.1.4 — 2026-10-05

- **Remembers the selected device** across restarts, and **refreshes automatically**
  when audio devices are plugged/unplugged (falls back to the Z Cinéma / first device
  if the current one disappears).
- **Per-device APO status:** switching devices re-checks whether Equalizer APO is
  attached and warns in the status line if it is not.
- **Tray quick-switch:** a **Presets** submenu (6 presets + Custom 1/2/3), plus
  **About**, **Start minimized**, **Back up settings…**, **Restore settings…** and
  **Open data folder**.
- **Backup & restore:** one `zcinema-backup.json` captures the profile, custom slots,
  remote bindings, per-device profiles and settings.
- Housekeeping: fixed a nullable warning in the Remote grid painting.

**Per-device note:** profiles are stored and applied per output device, but Equalizer
APO 1.4.2 runs a single config (scoped with `Device:` / `If`). Wiring that scoping up
— with an `Else` fallback so audio can never go silent — is the planned follow-up.

## v0.1.3 — 2026-10-05

- **Per-device profiles:** each audio device now keeps its own bass/treble/
  dialogue/width/EQ/ceiling (stored per endpoint GUID). Pick the controlled
  device from a selector in the **Output** panel.
- **Endpoint name fixed:** devices are matched and labelled using the same names
  Windows shows (`Speakers (Z Cinéma)`). The Core Audio property store returned
  empty strings, so matching had been silently falling back to the default
  device.
- **Remote mapping import / export** (JSON) on the Remote tab.
- **Lighter installer:** `ZCinemaSound-Setup-lite.exe` (framework-dependent; needs
  the .NET 10 Desktop Runtime) alongside the self-contained `ZCinemaSound-Setup.exe`.
- CI: GitHub Actions bumped to Node 24-native majors; more Core unit tests.

## v0.1.2 — 2026-10-05

- **Installer:** the post-install **Open Equalizer APO Device Selector** step now
  runs elevated, fixing the *"requested operation requires elevation"* error.
- **App:** the tray's **Open Equalizer APO Device Selector** action requests
  elevation explicitly.

## v0.1.1 — 2026-10-05

Layout fixes plus Equalizer APO attachment awareness.

- **Close (X) now minimizes to the tray** instead of quitting. Use the tray menu
  or the Remote tab's **Exit** to close completely.
- **Custom slots now Load as well as Save** (Save / Load 1‑2‑3), aligned with the
  preset row.
- **Equalizer APO attachment check:** the app detects whether APO is attached to
  the Z Cinéma endpoint (via its endpoint GUID) and shows a note plus a tray item
  — **Open Equalizer APO Device Selector** — when it isn't.
- CI: resolve the version from the pushed tag and opt into Node 24 for
  JavaScript actions.

## v0.1.0 — 2026-10-05

First release of the native app (and the project's move from PowerShell to C#).

**App (C#/.NET 10, WinForms)**
- Themed, frameless tray app with **Sound** and **Remote** tabs.
- **Sound:** Windows volume + mute (controls the Z Cinéma endpoint), ceiling
  (preamp) with a **calibration** dialog, Bass/Treble/Dialogue/Width, an **8-band
  graphic EQ**, built-in presets and **3 custom slots** (save/load).
- **Remote:** user-mode HID bridge runs inside the app; map the Media Center
  keys, **Learn** mode, and actions: `preset`, `custom`, `gui`, `bypass`,
  `sound`, `media`, `app`, `script`, `url`, `keys`.
- **Tray:** close-to-tray, single instance with focus-on-relaunch,
  **Start with Windows**, and setup verbs (**Install / update**, **Bypass**,
  **Uninstall**, **Device Selector**).

**Packaging**
- **Inno Setup installer** (detects Equalizer APO, which is **not bundled**;
  links to its download instead) plus a **portable single-file** `.exe`.
- **GitHub Actions** release workflow (test → publish → installer → release).

**Docs**
- `docs/PORTING.md` (parity spec), `docs/REMOTE-CODES.md` (HID protocol),
  icon/palette, and the README "Install (recommended)" section.

**Legacy**
- The PowerShell toolkit is retained as a reference/regression harness.

**Known limitations**
- Unsigned (SmartScreen may warn).
- Not attached detection: if Equalizer APO isn't attached to the endpoint the app
  shows a note and offers its Device Selector.
