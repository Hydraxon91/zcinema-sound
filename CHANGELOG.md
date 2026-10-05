# Changelog

## v0.4.0 — 2026-10-05

- **Global hotkeys:** `Ctrl+Alt+1..6` apply the presets and `Ctrl+Alt+0` opens the
  panel (tray toggle, off by default).
- **Custom slot management:** rename or clear the three slots
  (tray → *Manage custom slots…*); names show in the tray **Presets** menu.
- **Update check:** the app checks GitHub Releases on launch (tray balloon) and the
  tray menu has *Check for updates…*.
- **Live panel refresh:** remote preset / custom / sound actions now update the open
  panel immediately.
- **Bypass** is now a clear, stateful toggle in the tray and is shown in the status
  line.
- **Per-user install:** the installer now offers **Install for all users** vs **Install
  for me only** in the UI (and `ZCinemaSound-Setup.exe /CURRENTUSER` still works),
  installing to `%LocalAppData%\Programs` with no admin prompt when per-user.
- **Legacy PowerShell toolkit moved to `legacy\`** and marked unsupported; docs updated.
- Tests: 31.

## v0.3.1 — 2026-10-05

- **Starts in the tray by default.** The control panel no longer pops up in the
  middle of the screen on launch — the app goes straight to the notification area
  and the remote mappings keep working. Open it from the tray (double-click the
  icon, or **Open control panel**); launching the shortcut again while it's already
  running also brings the window up.
- Tick **Show window on start** in the tray menu if you'd rather it open on launch.

## v0.3.0 — 2026-10-05

- **High-DPI support:** the app is now `PerMonitorV2`-aware and scales its layout,
  fonts and custom-drawn controls (`LedSlider`, `GlassPanel`, `SegmentedControl`,
  `ThemedDropDown`) by the monitor DPI, so it grows proportionally and stays crisp
  above 100% scaling. At 100% it is pixel-identical to before.
- Scaled fonts are cached and the volume poll only repaints when something changes,
  so interaction stays smooth at high DPI. (Verified by the user at 125%/150%.)

## v0.2.1 — 2026-10-05

- **First-run guidance:** if Equalizer APO isn't installed, the status line says so
  and a one-time prompt offers its download page.
- **Cleaner uninstall:** the uninstaller now asks whether to also remove your
  `%APPDATA%\ZCinemaSound` data (remote mappings, per-device profiles, custom slots,
  backups) — choose No to keep them for a reinstall.
- Docs: Remote-tab screenshot; per-device scoping note.

**Known limitation:** the window uses a fixed pixel layout, so display scaling above
100% can look small/blurry. A DPI (PerMonitorV2) pass is planned — it needs a
high-DPI display to verify.

## v0.2.0 — 2026-10-05

- **Per-device EQ scoping (Equalizer APO):** with two or more device profiles, the
  generated config now contains one `If`/`ElseIf` block per device (matched on the
  endpoint GUID via `deviceGuid`), so every device gets its own tuning
  **simultaneously** instead of fighting over one global profile.
- An **`Else` fallback** applies the active device's profile to anything unmatched,
  so audio can never go silent — and a single device still writes the plain profile
  exactly as before.
- The app is now the source of truth (device-profile store + settings); the
  remote-action path writes through the same `ProfileStore`.
- New tray toggle **Per-device EQ scoping** (default on) to A/B by ear.
- Unit tests: 26 (was 22).

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
