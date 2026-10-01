# Mac Keys Remapper for Windows (Boot Camp)

Remaps internal Mac keyboard keys on Windows 10/11 running via Boot Camp:
- **Option (Alt) → Win**
- **Command (Win) → Alt**

**Does NOT affect external Windows keyboards** — they keep their normal layout.

## How It Works

Uses the [Interception](https://github.com/oblitum/Interception) keyboard driver to intercept and remap keys at the driver level, per-device. This means:
- Only the internal Mac keyboard is remapped
- External keyboards pass through unchanged
- Works system-wide, in all applications
- No reboot required after installation

## Prerequisites

1. **Windows 10/11** (Boot Camp)
2. **Interception driver** installed

## Installation

### Step 1: Install Interception Driver (Required)

The Interception driver is a low-level keyboard/mouse driver that allows per-device key remapping.

1. Download Interception from https://github.com/oblitum/Interception/releases
2. Extract the archive
3. Open **Command Prompt as Administrator**
4. Run:
   ```
   install-interception.exe /install
   ```
5. **Reboot your computer**

> **Note:** The driver is safe and open-source. It has been used by many projects for years. You can uninstall it anytime with `install-interception.exe /uninstall` followed by a reboot.

### Step 2: Download and Run

1. Download the latest release from the [Releases](https://github.com/deanluka/mackeys/releases) page
2. Extract `MacKeysRemap.exe` to a folder of your choice
3. **Right-click** `MacKeysRemap.exe` and select **"Run as administrator"**

Or from Command Prompt (as Administrator):
```bash
MacKeysRemap.exe
```

## Usage

### Default Usage

The app automatically detects the internal Mac keyboard by looking for "Apple" in the device name:

```bash
MacKeysRemap.exe
```

### Specify Keyboard Name

If auto-detection fails, specify the exact keyboard name:

```bash
MacKeysRemap.exe "Apple Internal Keyboard"
```

### Find Your Keyboard Name

Run the app without arguments — it will list all detected keyboards:

```
Available keyboards:
  - Apple Internal Keyboard (ID: 1)
  - Logitech USB Keyboard (ID: 2)
```

## Building from Source

### Prerequisites
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later

### Build
```bash
cd mackeys-windows
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

The compiled exe will be in the `publish` folder.

## Auto-Start on Boot

### Method 1: Task Scheduler

1. Open Task Scheduler
2. Create Basic Task
3. Trigger: "When I log on"
4. Action: "Start a program"
5. Program: `C:\path\to\MacKeysRemap.exe`
6. Check "Run with highest privileges"

### Method 2: Startup Folder

1. Press `Win+R`, type `shell:startup`, press Enter
2. Create a shortcut to `MacKeysRemap.exe`
3. Right-click shortcut → Properties → Advanced → Check "Run as administrator"

## Troubleshooting

### "Failed to create Interception context"
- Make sure you installed the Interception driver and rebooted
- Run `install-interception.exe /install` again as Administrator

### "Could not find internal keyboard"
- Run the app to list available keyboards
- Use the exact name as a command line argument

### Keys not remapping
- Make sure you're running as Administrator
- Check that the correct keyboard is detected
- Try specifying the keyboard name manually

## Uninstallation

1. Stop the app (Ctrl+C or close window)
2. Open Command Prompt as Administrator
3. Run:
   ```
   install-interception.exe /uninstall
   ```
4. Reboot your computer

## License

MIT
