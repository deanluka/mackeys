# MacKeysRemap

Per-device key remapper for Windows (Boot Camp). Remaps internal Mac keyboard without affecting external keyboards.

## Two Apps

| App | Exe Name | Purpose |
|-----|----------|---------|
| **Non-GUI** | `mackeysremap.exe` | Lightweight console app — runs in background, logs to file |
| **GUI** | `MacKeysRemap.exe` | Full GUI — tray icon, visual config, key capture |

## How It Works

Uses the [Interception](https://github.com/oblitum/Interception) keyboard driver to intercept and remap keys at the driver level, per-device:

- Only the internal Mac keyboard is remapped
- External keyboards pass through unchanged
- Works system-wide, in all applications
- Media keys (volume, mute, play/pause) via WH_KEYBOARD_LL hook

## Prerequisites

1. **Windows 10/11** (Boot Camp)
2. **Interception driver** installed

### Install Interception Driver

1. Download from https://github.com/oblitum/Interception/releases
2. Extract and open **Command Prompt as Administrator**
3. Run:
   ```
   install-interception.exe /install
   ```
4. **Reboot your computer**

## Non-GUI App (mackeysremap.exe)

### Usage

```bash
# Run with logs on screen (console mode)
mackeysremap.exe --console

# Run with logs to file (MacKeysRemap.log in same folder)
mackeysremap.exe
```

### Behavior

- Loads remapping rules from `config.json`
- Logs to `MacKeysRemap.log` (or screen with `--console`)
- No tray icon, no window — runs silently in background
- Press Ctrl+C to exit

## GUI App (MacKeysRemap.exe)

### Usage

1. Run `MacKeysRemap.exe` as Administrator
2. Window opens → loads keyboards → auto-starts mapping
3. Right-click tray icon → **Auto-start** to toggle Task Scheduler
4. Left-click tray icon → show/hide GUI

### Features

- Visual keyboard selection
- Key capture (press any key to capture)
- Per-device remapping
- Auto-start via Task Scheduler
- Media key remapping

## Configuration (config.json)

```json
[
  {
    "Keyboard": "Apple",
    "From": "LAlt",
    "To": "LWin"
  },
  {
    "Keyboard": "Apple",
    "From": "LWin",
    "To": "LAlt"
  }
]
```

| Field | Description |
|-------|-------------|
| `Keyboard` | Keyboard name, "All Keyboards", or substring match |
| `From` | Source key (e.g., "LAlt", "LWin", "F1", "VolumeUp") |
| `To` | Target key |

### Available Keys

- **Modifiers:** LAlt, RAlt, LWin, RWin, LCtrl, RCtrl, LShift, RShift
- **Function:** F1-F12
- **Navigation:** Insert, Delete, Home, End, PageUp, PageDown
- **Media:** VolumeUp, VolumeDown, VolumeMute, PlayPause, NextTrack, PrevTrack, Stop
- **Other:** PrintScreen, ScrollLock, Pause, CapsLock, NumLock, Escape, Space, Tab, Enter, Backspace

## Building from Source

### Prerequisites
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later

### Build Non-GUI App

```bash
cd mackeysremap
dotnet publish -c Release -r win-x64 --self-contained true -o publish
```

Output: `publish\mackeysremap.exe`

### Build GUI App

```bash
cd mackeysremap\MacKeysRemapGui
dotnet publish -c Release -r win-x64 --self-contained true -o publish
```

Output: `publish\MacKeysRemap.exe`

## Libraries Used

| Library | Purpose |
|---------|---------|
| **Interception** | Low-level keyboard driver for per-device remapping |
| **System.Text.Json** | Config file parsing (GUI app) |
| **System.Drawing** | Icon handling |
| **Windows Forms** | GUI (GUI app only) |

## Auto-Start (GUI App Only)

The GUI app can create a Task Scheduler task to run on login:

1. Right-click tray icon → **Auto-start**
2. Task created: `MacKeysRemap` (runs `MacKeysRemap.exe /tray`)
3. Click again to remove task

## Troubleshooting

### "Interception driver not found"
- Install Interception driver (see above)
- Reboot after installation

### "No keyboards found"
- Make sure you're running as Administrator
- Check that keyboards are connected

### Keys not remapping
- Check `config.json` format
- Verify keyboard name matches (use "All Keyboards" for testing)
- Check log file for match messages

## License

MIT
