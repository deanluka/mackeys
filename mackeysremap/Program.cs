using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;

namespace MacKeysRemap;

class Program
{
    private const string InterceptionDll = "interception.dll";
    private const int INTERCEPTION_KEYBOARD = 1;
    private const int INTERCEPTION_MAX_KEYBOARD = 10;
    private const ushort INTERCEPTION_FILTER_KEYBOARD_ALL = 0xFFFF;

    [DllImport(InterceptionDll, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr interception_create_context();

    [DllImport(InterceptionDll, CallingConvention = CallingConvention.Cdecl)]
    private static extern void interception_destroy_context(IntPtr context);

    [DllImport(InterceptionDll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int interception_send(IntPtr context, int device, ref KeyStroke stroke, int n);

    [DllImport(InterceptionDll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int interception_receive(IntPtr context, int device, ref KeyStroke stroke, int n);

    [DllImport(InterceptionDll, CallingConvention = CallingConvention.Cdecl)]
    private static extern void interception_set_filter(IntPtr context, Predicate predicate, ushort filter);

    private delegate int Predicate(int device);

    [DllImport(InterceptionDll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int interception_is_keyboard(int device);

    [DllImport(InterceptionDll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int interception_get_hardware_id(int device, IntPtr buffer, int size);

    [DllImport(InterceptionDll, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr interception_wait_with_timeout(IntPtr context, int milliseconds);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    private const uint INJECTED_EXTRA_INFO = 0xA55C1E00;

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyStroke
    {
        public ushort Code;
        public ushort State;
        public uint Information;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    private static IntPtr _context = IntPtr.Zero;
    private static IntPtr _hookId = IntPtr.Zero;
    private static LowLevelKeyboardProc? _hookProc;
    private static List<KeyRemapping> _config = new();
    private static string _logPath = "";
    private static bool _consoleMode = false;
    private static string _lastActiveDevice = "";

    public class KeyRemapping
    {
        public string Keyboard { get; set; } = "";
        public string From { get; set; } = "";
        public string To { get; set; } = "";
    }

    static void Main(string[] args)
    {
        // Console mode: show logs on screen
        _consoleMode = args.Length == 0 || args.Contains("--console");

        _logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MacKeysRemap.log");

        Log("========================================");
        Log("  MacKeysRemap - Per-Device Key Remapper");
        Log("========================================");
        Log("");

        if (!IsAdministrator())
        {
            Log("ERROR: This application requires administrator privileges.");
            Log("Please run as Administrator.");
            if (_consoleMode) Console.ReadKey();
            return;
        }

        // Load config
        LoadConfig();

        if (_config.Count == 0)
        {
            Log("No remapping rules found in config.json");
            if (_consoleMode) Console.ReadKey();
            return;
        }

        Log($"Loaded {_config.Count} remapping rules:");
        foreach (var r in _config)
        {
            Log($"  [{r.Keyboard}] {r.From} -> {r.To}");
        }
        Log("");

        // Create Interception context
        _context = interception_create_context();
        if (_context == IntPtr.Zero)
        {
            Log("ERROR: Interception driver not found or not loaded.");
            Log("Please install Interception driver:");
            Log("1. Download from https://github.com/oblitum/Interception");
            Log("2. Run install-interception.exe /install");
            Log("3. Reboot your computer");
            if (_consoleMode) Console.ReadKey();
            return;
        }

        // Set filter for all keyboards
        Predicate isKeyboard = (device) => interception_is_keyboard(device);
        interception_set_filter(_context, isKeyboard, INTERCEPTION_FILTER_KEYBOARD_ALL);

        // Enumerate keyboards
        int detectedCount = 0;
        for (int i = 1; i <= INTERCEPTION_MAX_KEYBOARD; i++)
        {
            if (interception_is_keyboard(i) == 1)
            {
                string hwId = GetHardwareId(i);
                string name = string.IsNullOrEmpty(hwId) ? $"Keyboard {i}" : GetFriendlyName(hwId, i);
                Log($"Found keyboard {i}: {name} ({hwId})");
                detectedCount++;
            }
        }

        if (detectedCount == 0)
        {
            Log("ERROR: No keyboards found.");
            interception_destroy_context(_context);
            if (_consoleMode) Console.ReadKey();
            return;
        }

        Log($"Total keyboards detected: {detectedCount}");
        Log("");

        // Install WH_KEYBOARD_LL hook for media keys
        _hookProc = HookCallback;
        _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _hookProc, GetModuleHandle(null), 0);
        if (_hookId == IntPtr.Zero)
        {
            Log("WARNING: Failed to install low-level keyboard hook. Media keys will not be remapped.");
        }
        else
        {
            Log("Low-level keyboard hook installed for media keys.");
        }

        Log("");
        Log("Remapping is active. Press Ctrl+C to exit.");
        Log("");

        // Start remap loop
        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        try
        {
            RunRemapLoop(cts.Token);
        }
        catch (OperationCanceledException)
        {
            Log("");
            Log("Shutting down...");
        }

        // Cleanup
        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
        }
        interception_destroy_context(_context);
        Log("Done.");
    }

    private static void LoadConfig()
    {
        string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
        if (!File.Exists(configPath))
        {
            Log("Config file not found: " + configPath);
            return;
        }

        try
        {
            string json = File.ReadAllText(configPath);
            _config = JsonSerializer.Deserialize<List<KeyRemapping>>(json) ?? new List<KeyRemapping>();
        }
        catch (Exception ex)
        {
            Log("Error loading config: " + ex.Message);
        }
    }

    private static string GetHardwareId(int deviceId)
    {
        var buffer = Marshal.AllocHGlobal(1024);
        try
        {
            int len = interception_get_hardware_id(deviceId, buffer, 1024);
            return len > 0 ? Marshal.PtrToStringUni(buffer) ?? "" : "";
        }
        catch { return ""; }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    private static string GetFriendlyName(string hwId, int deviceId)
    {
        if (hwId.Contains("VID_"))
        {
            var parts = hwId.Split('&');
            if (parts.Length > 0) return parts[0];
        }
        return hwId;
    }

    private static void RunRemapLoop(CancellationToken ct)
    {
        var stroke = new KeyStroke();

        while (!ct.IsCancellationRequested)
        {
            // Wait for any device to have input ready
            int device = (int)(uint)interception_wait_with_timeout(_context, 50);
            if (device <= 0) continue;

            if (interception_receive(_context, device, ref stroke, 1) <= 0) continue;

            string keyboardName = GetFriendlyName(GetHardwareId(device), device);
            _lastActiveDevice = keyboardName;

            // Check remapping rules
            foreach (var remap in _config)
            {
                if (!MatchesKeyboard(remap.Keyboard, device, GetHardwareId(device), keyboardName)) continue;

                ushort fromCode = GetScanCode(remap.From);
                bool fromE0 = remap.From.StartsWith("E0");

                if (fromCode == stroke.Code)
                {
                    ushort toCode = GetScanCode(remap.To);
                    stroke.Code = toCode;

                    bool isKeyUp = (stroke.State & 0x01) != 0;
                    if (!isKeyUp)
                    {
                        Log($"[MATCH] [{keyboardName}] {remap.From} -> {remap.To}");
                    }
                    break;
                }
            }

            interception_send(_context, device, ref stroke, 1);
        }
    }

    private static bool MatchesKeyboard(string configKeyboard, int deviceId, string hardwareId, string friendlyName)
    {
        if (string.IsNullOrWhiteSpace(configKeyboard)) return true;
        if (configKeyboard.Equals("All", StringComparison.OrdinalIgnoreCase) ||
            configKeyboard.Equals("All Keyboards", StringComparison.OrdinalIgnoreCase)) return true;
        if (configKeyboard.Equals(friendlyName, StringComparison.OrdinalIgnoreCase)) return true;
        if (!string.IsNullOrEmpty(hardwareId) && hardwareId.Contains(configKeyboard, StringComparison.OrdinalIgnoreCase)) return true;
        if (friendlyName.Contains(configKeyboard, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static ushort GetScanCode(string keyName)
    {
        return keyName.ToUpper() switch
        {
            "LALT" => 0x38,
            "RALT" => 0xE038,
            "LWIN" => 0x5B,
            "RWIN" => 0xE05C,
            "LCTRL" => 0x1D,
            "RCTRL" => 0xE01D,
            "LSHIFT" => 0x2A,
            "RSHIFT" => 0x36,
            "F1" => 0x3B,
            "F2" => 0x3C,
            "F3" => 0x3D,
            "F4" => 0x3E,
            "F5" => 0x3F,
            "F6" => 0x40,
            "F7" => 0x41,
            "F8" => 0x42,
            "F9" => 0x43,
            "F10" => 0x44,
            "F11" => 0x57,
            "F12" => 0x58,
            "INSERT" => 0xE052,
            "DELETE" => 0xE053,
            "HOME" => 0xE047,
            "END" => 0xE04F,
            "PAGEUP" => 0xE049,
            "PAGEDOWN" => 0xE051,
            "PRINTSCREEN" => 0xE037,
            "SCROLLLOCK" => 0x46,
            "PAUSE" => 0xE045,
            "CAPSLOCK" => 0x3A,
            "NUMLOCK" => 0x45,
            "ESCAPE" => 0x01,
            "SPACE" => 0x39,
            "TAB" => 0x0F,
            "ENTER" => 0x1C,
            "BACKSPACE" => 0x0E,
            _ => 0x00
        };
    }

    private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = wParam.ToInt32();
            bool isDown = msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN;
            bool isUp = msg == WM_KEYUP || msg == WM_SYSKEYUP;

            if (!isDown && !isUp) return CallNextHookEx(_hookId, nCode, wParam, lParam);

            var kbData = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            bool isMediaKey = kbData.vkCode >= 0xAD && kbData.vkCode <= 0xB7;

            if (!isMediaKey) return CallNextHookEx(_hookId, nCode, wParam, lParam);

            // Skip injected keys
            if (kbData.dwExtraInfo == (UIntPtr)INJECTED_EXTRA_INFO) return CallNextHookEx(_hookId, nCode, wParam, lParam);

            string keyName = GetKeyNameFromVK((byte)kbData.vkCode);

            foreach (var remap in _config)
            {
                if (!remap.From.Equals(keyName, StringComparison.OrdinalIgnoreCase)) continue;

                ushort toCode = GetScanCode(remap.To);
                if (toCode == 0) continue;

                uint flags = 0;
                if (isUp) flags |= KEYEVENTF_KEYUP;
                if (toCode > 0xFF) flags |= KEYEVENTF_EXTENDEDKEY;

                byte vk = GetVKFromScanCode(toCode);
                keybd_event(vk, (byte)toCode, flags, (UIntPtr)INJECTED_EXTRA_INFO);

                if (isDown) Log($"[MEDIA] {keyName} -> {remap.To}");
                return (IntPtr)1;
            }

            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }
        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private static string GetKeyNameFromVK(byte vk)
    {
        return vk switch
        {
            0xAD => "VolumeMute",
            0xAE => "VolumeDown",
            0xAF => "VolumeUp",
            0xB0 => "NextTrack",
            0xB1 => "PrevTrack",
            0xB2 => "Stop",
            0xB3 => "PlayPause",
            _ => $"VK_0x{vk:X2}"
        };
    }

    private static byte GetVKFromScanCode(ushort scanCode)
    {
        return scanCode switch
        {
            0x38 => 0xA4, // LAlt -> LWin (VK_MENU)
            0xE038 => 0xA5, // RAlt -> RWin
            0x5B => 0xA4, // LWin -> LAlt (VK_LWIN -> VK_MENU)
            0xE05C => 0xA5, // RWin -> RAlt
            0x1D => 0xA2, // LCtrl
            0xE01D => 0xA3, // RCtrl
            0x2A => 0xA0, // LShift
            0x36 => 0xA1, // RShift
            0x3B => 0x70, // F1
            0x3C => 0x71, // F2
            0x3D => 0x72, // F3
            0x3E => 0x73, // F4
            0x3F => 0x74, // F5
            0x40 => 0x75, // F6
            0x41 => 0x76, // F7
            0x42 => 0x77, // F8
            0x43 => 0x78, // F9
            0x44 => 0x79, // F10
            0x57 => 0x7A, // F11
            0x58 => 0x7B, // F12
            0xE052 => 0x2D, // Insert
            0xE053 => 0x2E, // Delete
            0xE047 => 0x24, // Home
            0xE04F => 0x23, // End
            0xE049 => 0x21, // PageUp
            0xE051 => 0x22, // PageDown
            0xE037 => 0x2C, // PrintScreen
            0x46 => 0x91, // ScrollLock
            0xE045 => 0x13, // Pause
            0x3A => 0x14, // CapsLock
            0x45 => 0x90, // NumLock
            0x01 => 0x1B, // Escape
            0x39 => 0x20, // Space
            0x0F => 0x09, // Tab
            0x1C => 0x0D, // Enter
            0x0E => 0x08, // Backspace
            _ => 0
        };
    }

    private static void Log(string message)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss");
        string logMessage = $"[{timestamp}] {message}";

        if (_consoleMode)
        {
            Console.WriteLine(logMessage);
        }
        else
        {
            try
            {
                File.AppendAllText(_logPath, logMessage + Environment.NewLine);
            }
            catch { }
        }
    }

    private static bool IsAdministrator()
    {
        var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        var principal = new System.Security.Principal.WindowsPrincipal(identity);
        return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
    }
}
