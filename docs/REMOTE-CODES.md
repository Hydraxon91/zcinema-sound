# Z Cinema remote — HID button map

Reverse-engineered from `tools\Remote-Probe.ps1` captures on this machine.
No driver involved: these are plain HID input reports read in user mode.

## Collections (`USB\VID_046D&PID_0A0F&MI_02`)

| Collection | Usage | Report IDs seen | Notes |
|---|---|---|---|
| `COL01` Consumer | page `0x0C` usage `0x01` | `0x01` | Remote buttons (bitmask) and Media/Preset codes |
| `COL02` Vendor | page `0xFFBC` usage `0x88` | `0x02` | Media Center button bitmask |
| `COL03` Keyboard | page `0x01` usage `0x06` | — | Needs Administrator to read; not observed to carry buttons |

Reports are 64 bytes; unused bytes are `00`. Press = bits set, release = all-zero report.

## COL02 Vendor — report `0x02`, byte 1 = bitmask

| Bit | Button |
|---|---|
| `0x01` | Music |
| `0x02` | Videos |
| `0x04` | Pictures |
| `0x08` | Media Player (the green "Windows" button) |
| `0x10` | DVD menu |
| `0x20` | Live TV |
| `0x40` | Recorded TV |

`02 00 …` = release. This collection is ignored by Windows, so it is the cleanest
hook for custom actions on these seven buttons.

## COL01 Consumer — report `0x01`

### A) Media / Preset codes — bytes `[8]=0xB1`, `[9]=<code>`

Report shape: `01 00 00 00 00 00 00 02 B1 <code> …`

| Code | Button |
|---|---|
| `0x11` | Display |
| `0x2E` | Preset 1 |
| `0x2F` | Preset 2 |
| `0x30` | Preset 3 |
| `0x31` | Preset 4 |
| `0xD8` | Music |
| `0xD9` | Videos |
| `0xDA` | Pictures |
| `0xDB` | Media Player (Windows) |
| `0xDC` | DVD menu |
| `0xDD` | Live TV |
| `0xDE` | Recorded TV |

Notably, **Preset 1-4 and Display** have no Windows action, so they are free to
claim. The media buttons also appear here (usually alongside a COL02 bit).

### B) Remote button bitmask — bytes 1..3

| Byte | Bit `0x80` | `0x40` | `0x20` | `0x10` | `0x08` | `0x04` | `0x02` | `0x01` |
|---|---|---|---|---|---|---|---|---|
| 1 | Stop | Play | Replay | Skip (next) | Rewind | Forward | — | — |
| 2 | — | — | — | — | Pause | Mute | Ch− | Ch+ |
| 3 | — | Full screen | — | — | Info | Record | Guide | Back |

Volume Up/Down were not isolated (Windows handles volume before we see a
distinct report). Play/Pause/Stop/Skip/Replay/Rewind/Forward, Ch+/Ch−, Back,
Guide, Info, Record and Full screen all work in Windows already.

## Windows behaviour observed

- The **green "Windows" (Media Player) button** opens the default media app
  (Consumer `0xDB` + Vendor `0x08`) — i.e. the OS acts on it, so claiming it may
  cause a double action unless we hide/redirect it (needs a kernel tool — we
  won't).
- **Preset 1-4** and **Display** produced no visible Windows action → ideal
  targets for custom mappings (presets, opening the GUI, etc.).
- Volume/Mute/transport already work and should be left to Windows.

## Suggested custom mapping (all user-mode, no driver)

| Remote button | Action |
|---|---|
| Preset 1 | Load Music preset |
| Preset 2 | Load Movies preset |
| Preset 3 | Load Vocal preset |
| Preset 4 | Load V-Shape preset |
| Display | Open / focus the control panel |
| Media Player (Windows) | Open the control panel (may double-fire with the OS) |
| Music / Videos / Pictures | Launch a chosen app, or load a preset |

Future work: a background **remote bridge** (see `docs/ROADMAP.md`) that reads
COL01/COL02, matches the codes above, and runs a user-editable mapping from a
JSON file, with a **Learn** mode in the GUI.
