# MacKeysRemap GUI

Per-device key remapper for Windows (Boot Camp) — GUI version with tray icon and visual configuration.

## Usage

1. Run `MacKeysRemap.exe` as Administrator
2. Window opens → loads keyboards → auto-starts mapping
3. Right-click tray icon → **Auto-start** to toggle Task Scheduler
4. Left-click tray icon → show/hide GUI

## Building

```bash
cd MacKeysRemapGui
dotnet publish -c Release -r win-x64 --self-contained true -o publish
```

Output: `publish\MacKeysRemap.exe`

## Features

- Visual keyboard selection
- Key capture (press any key to capture)
- Per-device remapping
- Auto-start via Task Scheduler
- Media key remapping
- Resizable window

## Configuration

Edit `config.json` in the publish folder:

```json
[
  {
    "Keyboard": "Apple",
    "From": "LAlt",
    "To": "LWin"
  }
]
```

## Libraries

- **Interception** — low-level keyboard driver
- **System.Text.Json** — config parsing
- **System.Drawing** — icon handling
- **Windows Forms** — GUI
