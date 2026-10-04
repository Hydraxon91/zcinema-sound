# Troubleshooting

## Nothing changes when I edit the profile
- Equalizer APO has to be attached to **Speakers (Z Cinema)**. Run
  `C:\Program Files\EqualizerAPO\DeviceSelector.exe (or Configurator.exe on older builds)`, tick the device, reboot.
- Check `config.txt` actually contains `Include: ZCinema.txt` (the installer adds it).
- Confirm the app plays through the Z Cinema (Sound → set as default).
- ASIO / WASAPI-exclusive apps bypass APO processing — use the shared-mode
  (default) output.

## The installer says Equalizer APO is not attached
That is expected before you use the Configurator. Do that first, reboot, re-run
`src\Install-ZCinema.ps1`. Use `-Force` to copy the profile anyway.

## Volume feels too quiet / too loud overall
That is the `Preamp:` line — it is the ceiling. Run
`tools\Calibrate-Ceiling.ps1` and apply the suggested value, or edit
`Preamp:` by hand (−6 to −13 dB is the usual range).

## Crackling or distortion on loud bass
Lower `Preamp:` so it is at least as negative as your biggest positive filter
gain (boosts need headroom). Example: +6 dB bass shelf wants about −7 dB preamp.

## Stereo sounds odd / collapses in mono
Reduce the crossfeed `g` in the `Copy:` lines (0.10 → 0.05), or comment both
lines out.

## It stopped working after a Windows update
Re-open the Equalizer APO Configurator and confirm the device still shows
"APO is already installed"; reboot if needed.

## Where are the logs?
`C:\Windows\ServiceProfiles\LocalService\AppData\Local\Temp\EqualizerAPO.log`

## Undo everything
```powershell
powershell -ExecutionPolicy Bypass -File .\src\Uninstall-ZCinema.ps1
```
Then optionally untick the device in the Configurator.

## Audio went silent right after installing
A malformed profile can make Equalizer APO mute the device. Restore sound
immediately with:
```powershell
powershell -ExecutionPolicy Bypass -File .\src\Bypass-ZCinema.ps1
```
Then re-check the profile (or the Equalizer APO log via
`tools\Get-EqualizerApoLog.ps1`). A reboot is required after first attaching the
APO in DeviceSelector.

## The GUI can't save / sliders do nothing
The profile lives under `Program Files`, which needs write permission. The
installer grants it; if you skipped that, either re-run
`src\Install-ZCinema.ps1` once, or launch the GUI and accept the UAC prompt (it
self-elevates if it cannot write).

If the sliders move but you hear no change, the profile is not enabled: open
`config.txt` and make sure it contains `Include: ZCinema.txt` (the GUI shows a
red warning in this case).
