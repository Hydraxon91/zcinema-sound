# Presets

Everything here is available from the app's **Sound** tab (legacy equivalent:
`legacy\tools\ZCinema-GUI.ps1`).

## Built-in presets (each also sets the EQ)

| Button | Bass | Treble | Dialogue | Width | EQ shape |
|---|---|---|---|---|---|
| **Flat**   | 0  | 0 | 0  | 0%  | all flat |
| **Music**  | +6 | +4 | 0  | 10% | gentle smile (low + high lift) |
| **Movies** | +4 | +2 | +5 | 20% | dialogue/presence lift |
| **Night**  | −2 | +2 | +3 | 10% | bass trimmed for neighbours |
| **Vocal**  | −2 | +2 | +7 | 5%  | mid/presence focus |
| **V-Shape**| +8 | +6 | −2 | 15% | classic loudness smile |

Width = stereo widening (negative crossfeed); 0% is mono-safe.

## Custom presets (1 / 2 / 3)

- **Left-click** a Custom button to recall that saved slot.
- **Right-click** it to save the current sliders into that slot.
- Saved to `%APPDATA%\ZCinemaSound\presets.json` (your user profile — no admin
  needed). A custom slot stores the full state: Ceiling, Bass, Treble, Dialogue,
  Width and all 8 EQ bands.

So a typical workflow: dial in a sound you like, right-click **Custom 1**; make
a movie setting, right-click **Custom 2**; and switch with a single click later.

## Manual add-on files (optional)

`presets\music-addon.txt`, `movies-addon.txt` and `night-addon.txt` are plain
Equalizer APO snippets that layer extra filters on top of the main profile:

```
Include: ZCinema.txt
Include: presets\movies-addon.txt
```

They stack, so use one at a time. If you prefer a file-based workflow, edit
`C:\Program Files\EqualizerAPO\config\ZCinema.txt`.

## Making your own (Equalizer APO syntax)

```
Filter: ON PK  Fc 1000 Hz Gain 3 dB Q 1.0     # peaking band (the reliable type)
Filter: ON LSC Fc 100 Hz  Gain 5 dB Q 0.7      # low shelf  (some builds ignore this)
Filter: ON HSC Fc 8000 Hz Gain 3 dB Q 0.7      # high shelf (some builds ignore this)
Preamp: -6 dB                                  # ceiling / headroom
```

Note: on this Equalizer APO build the `LSC`/`HSC` shelf types were ignored, which
is why the GUI uses broad **PK** bands for bass and treble. Keep `Preamp:` at
least as negative as your largest positive gain, or you will clip.
