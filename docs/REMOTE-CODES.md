# Z Cinema remote — HID protocol & button map

Reverse-engineered from `tools\Remote-Probe.ps1` captures on this machine.
No driver involved: these are plain HID input reports read in user mode.

## Collections (`USB\VID_046D&PID_0A0F&MI_02`)

| Collection | Usage | Report ID | Carries |
|---|---|---|---|
| `COL01` Consumer | page `0x0C` usage `0x01` | `0x01` | remote-button bitmask and `B1` media/preset codes |
| `COL02` Vendor | page `0xFFBC` usage `0x88` | `0x02` | Media Center button bitmask |
| `COL03` Keyboard | page `0x01` usage `0x06` | — | needs Administrator to read; not observed to carry buttons |

**Framing:** input reports are 64 bytes; unused bytes are `00`. A press sets bits
in the first bytes; a release is an all-zero report. Report ID is the first byte.

**One press can emit two reports.** Many buttons send a `COL01` bitmask report
*and* a `COL01 …B1…` code report (~25 ms apart), and some also send a `COL02`
bit. A decoder must map **both forms to the same button name** and de-duplicate a
button that repeats within ~700 ms, so a press fires exactly one action.

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

`02 00 …` = release. Windows ignores this collection, so it is the cleanest hook
for the seven Media Center buttons.

## COL01 Consumer — report `0x01`

### A) `B1` code table — bytes `[8]=0xB1`, `[9]=<code>`

Report shape: `01 00 00 00 00 00 00 02 B1 <code> …`

| Code | Button | Code | Button | Code | Button |
|---|---|---|---|---|---|
| `0xC2` | Forward | `0xC8` | Ch+ | `0xD3` | Info |
| `0xC3` | Rewind | `0xC9` | Ch− | `0xD6` | Full screen |
| `0xC4` | Skip | `0xCA` | Mute | `0xD8` | Music |
| `0xC5` | Replay | `0xCB` | Pause | `0xD9` | Videos |
| `0xC6` | Play | `0xD0` | Back | `0xDA` | Pictures |
| `0xC7` | Stop | `0xD1` | Guide | `0xDB` | Media Player |
| `0x11` | Display | `0xD2` | Record | `0xDC` | DVD menu |
| `0x2E` | Preset 1 | `0x2F` | Preset 2 | `0xDD` | Live TV |
| `0x30` | Preset 3 | `0x31` | Preset 4 | `0xDE` | Recorded TV |

Media/Preset/Display buttons appear **only** here (no bitmask report). Unknown
codes should be surfaced as `media 0xNN` for future discovery.

### B) Remote button bitmask — bytes 1..3

| Byte | `0x80` | `0x40` | `0x20` | `0x10` | `0x08` | `0x04` | `0x02` | `0x01` |
|---|---|---|---|---|---|---|---|---|
| 1 | Stop | Play | Replay | Skip (next) | Rewind | Forward | — | — |
| 2 | — | — | — | — | Pause | Mute | Ch− | Ch+ |
| 3 | — | Full screen | — | — | Info | Record | Guide | Back |

The transport / Ch± / Back / Guide / Info / Record / Full screen buttons emit
**both** this bitmask and a matching `B1` code above → decode both to the same
name and de-duplicate. Volume Up/Down were not isolated (Windows consumes them).

## Free vs Windows-handled

| Category | Buttons | Windows | App exposure |
|---|---|---|---|
| Free (Media Center leftovers) | Preset 1-4, Display, Guide, Info, Record, Full screen, Ch+, Ch−, DVD menu, Live TV, Recorded TV, Music, Videos, Pictures | nothing | **mappable** |
| Windows-handled | Play, Pause, Stop, Skip, Replay, Rewind, Forward, Mute, Back | acts | hidden (would double-fire) |
| App launcher | Media Player (green Windows) | opens a media app on some systems; nothing observed on this Win11 | exposed (user-requested) |

Mapping a Windows-handled key too would fire twice; we don't, and suppressing it
would need a kernel filter (out of scope — no driver, no test-signing).

## Reference app behaviour

`tools\ZCinema-GUI.ps1` hosts the reader in-process (tray app). Defaults:

| Remote button | Default action |
|---|---|
| Preset 1 | `preset:Music` |
| Preset 2 | `preset:Movies` |
| Preset 3 | `preset:Vocal` |
| Preset 4 | `preset:V-Shape` |
| Display | `gui` (open/focus the control panel) |
| everything else | `none` (user-mappable) |

Mappings live in `%APPDATA%\ZCinemaSound\remote.json`; the full C# parity
checklist is in `docs\PORTING.md`.
