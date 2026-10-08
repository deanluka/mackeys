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

    [DllImport(InterceptionDll, EntryPoint = "interception_get_hardware_id", CallingConvention = CallingConvention.Cdecl)]
    private static extern uint interception_get_hardware_id(IntPtr context, int device, IntPtr hardwareIdBuffer, uint bufferSize);

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
        Log("  MacKeysRemap Console - Per-Device Key Remapper");
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

        // Check for interception.dll
        if (!File.Exists("interception.dll"))
        {
            Log("ERROR: interception.dll not found in application directory.");
            Log("Please make sure interception.dll is in the same folder as mackeysremap.exe");
            if (_consoleMode) Console.ReadKey();
            return;
        }
        Log("interception.dll found.");

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
        Log("Interception driver found and loaded.");

        // Set filter for all keyboards
        Predicate isKeyboard = (device) => interception_is_keyboard(device);
        interception_set_filter(_context, isKeyboard, INTERCEPTION_FILTER_KEYBOARD_ALL);

        // Enumerate keyboards - only show real keyboards with hardware IDs
        int detectedCount = 0;
        for (int i = 1; i <= INTERCEPTION_MAX_KEYBOARD; i++)
        {
            if (interception_is_keyboard(i) == 1)
            {
                string hwId = GetHardwareId(i);
                if (!string.IsNullOrEmpty(hwId))
                {
                    string name = GetFriendlyName(hwId, i);
                    Log($"Found keyboard {i}: {name} ({hwId})");
                    detectedCount++;
                }
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
        var buffer = Marshal.AllocHGlobal(4096);
        try
        {
            uint result = interception_get_hardware_id(_context, deviceId, buffer, 4096);
            if (result > 0)
            {
                return Marshal.PtrToStringUni(buffer) ?? "";
            }
            return "";
        }
        catch { return ""; }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    private static string GetFriendlyName(string hwId, int deviceId)
    {
        if (string.IsNullOrEmpty(hwId)) return $"Keyboard {deviceId}";

        // Extract friendly name from hardware ID
        // ACPI\VEN_LEN&DEV_0071 -> "Internal / PS2 Keyboard"
        // HID\VID_046D&PID_C534 -> "Logitech Keyboard"
        if (hwId.StartsWith("ACPI\\"))
        {
            if (hwId.Contains("VEN_LEN")) return "Internal / PS2 Keyboard";
            return "ACPI Keyboard";
        }
        if (hwId.StartsWith("HID\\"))
        {
            if (hwId.Contains("VID_")) return "USB/HID Keyboard";
            return "HID Keyboard";
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

            // Log every key press with full info (same as GUI)
            bool isKeyUp = (stroke.State & 0x01) != 0;
            bool isE0 = (stroke.State & 0x02) != 0;
            bool isE1 = (stroke.State & 0x04) != 0;
            string keyName = KeyNames.GetKeyName(stroke.Code, isE0);
            string hwId = GetHardwareId(device);

            if (!isKeyUp)
            {
                Log($"[INT][Dev {device}][NO-MATCH] Device='{keyboardName}' (HWID='{hwId}') | Key='{keyName}' | Code=0x{stroke.Code:X2} | State=0x{stroke.State:X4} (UP={isKeyUp}, E0={isE0}, E1={isE1}) | Info=0x{stroke.Information:X8}");
            }

            // Check remapping rules - match by substring
            bool matched = false;
            foreach (var remap in _config)
            {
                if (!MatchesKeyboard(remap.Keyboard, device, hwId, keyboardName)) continue;

                var fromKey = KeyNames.FindKey(remap.From);
                if (fromKey == null) continue;
                ushort fromCode = fromKey.Code;
                bool fromE0 = fromKey.IsE0;

                if (fromCode == stroke.Code)
                {
                    var toKey = KeyNames.FindKey(remap.To);
                    if (toKey == null) continue;
                    stroke.Code = toKey.Code;

                    if (!isKeyUp)
                    {
                        Log($"[INT][Dev {device}][MATCH] Device='{keyboardName}' | ConfigKeyboard='{remap.Keyboard}' | {remap.From} (0x{fromCode:X2},E0={fromE0}) -> {remap.To} (0x{toKey.Code:X2}) | RawState=0x{stroke.State:X4}");
                    }
                    matched = true;
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

            string keyName = KeyNames.GetKeyNameFromVK((byte)kbData.vkCode);

            foreach (var remap in _config)
            {
                if (!remap.From.Equals(keyName, StringComparison.OrdinalIgnoreCase)) continue;

                var toKey = KeyNames.FindKey(remap.To);
                if (toKey == null) continue;
                ushort toCode = toKey.Code;

                uint flags = 0;
                if (isUp) flags |= KEYEVENTF_KEYUP;
                if (toKey.IsE0) flags |= KEYEVENTF_EXTENDEDKEY;

                byte vk = toKey.VirtualKey;
                keybd_event(vk, (byte)toCode, flags, (UIntPtr)INJECTED_EXTRA_INFO);

                if (isDown) Log($"[MEDIA] {keyName} -> {remap.To}");
                return (IntPtr)1;
            }

            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }
        return CallNextHookEx(_hookId, nCode, wParam, lParam);
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
