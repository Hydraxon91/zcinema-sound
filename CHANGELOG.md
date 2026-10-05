# Changelog

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
