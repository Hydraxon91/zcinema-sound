# Legacy PowerShell toolkit (unsupported)

The PowerShell scripts, `.bat` launchers and profile template that predate the
native app. They are kept only as a **reference / regression harness** and are no
longer maintained.

**Use the app instead:** `src-app/` — see the top-level `README.md`.

- `ZCinema.bat` — old menu launcher (double-click).
- `launchers/` — per-action `.bat` files, handy for desktop shortcuts.
- `src/` — Install / Uninstall / Bypass.
- `tools/` — the old GUI, remote probe/bridge, calibration, log fetch.
- `config/ZCinema.txt` — the profile template.
- `presets/` — optional add-on filter snippets.

These share the same Equalizer APO profile and the same single-instance mutex as
the app, so **don't run both at once**. Run everything from inside this folder —
the scripts resolve paths relative to their own location.

They use the in-box Windows PowerShell; there is no build step.
