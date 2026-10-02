using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace MacKeysRemapGui;

public class InterceptionRemapper : IDisposable
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;

    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint LLKHF_INJECTED = 0x00000010;

    // Signature to identify our injected keystrokes and prevent infinite remapping loops
    private const ulong INJECTED_EXTRA_INFO = 0xDEADBEEF;

    public event Action<string>? OnLog;

    private readonly List<KeyRemapping> _config;
    private IntPtr _context = IntPtr.Zero;
    private CancellationTokenSource? _cts;
    private Task? _remapTask;
    private readonly Dictionary<int, (string HardwareId, string FriendlyName)> _deviceCache = new();

    private IntPtr _hookId = IntPtr.Zero;
    private readonly LowLevelKeyboardProc _hookProc;
    private bool _disposed = false;

    public InterceptionRemapper(List<KeyRemapping> config)
    {
        _config = config;
        _hookProc = RemapHookCallback;
    }

    public bool Start()
    {
        _context = InterceptionNative.interception_create_context();
        if (_context == IntPtr.Zero) return false;

        // Filter all keyboard devices in driver
        InterceptionNative.interception_set_filter(
            _context,
            InterceptionNative.IsKeyboardPredicate,
            InterceptionNative.INTERCEPTION_FILTER_KEY_ALL);

        // Pre-populate device cache
        _deviceCache.Clear();
        for (int i = 1; i <= InterceptionNative.INTERCEPTION_MAX_KEYBOARD; i++)
        {
            if (InterceptionNative.interception_is_keyboard(i) == 1)
            {
                string hwId = InterceptionNative.GetHardwareId(_context, i);
                string friendly = InterceptionNative.GetFriendlyDeviceName(hwId, i);
                _deviceCache[i] = (hwId, friendly);
            }
        }

        // Install global low-level hook for multimedia / consumer keys that bypass kbdclass
        _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _hookProc, GetModuleHandle(null), 0);

        _cts = new CancellationTokenSource();
        _remapTask = Task.Run(() => RemapLoop(_cts.Token));
        return true;
    }

    public void Stop()
    {
        try
        {
            _cts?.Cancel();
            _remapTask?.Wait(1000);
        }
        catch { }
        finally
        {
            if (_hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }
            if (_context != IntPtr.Zero)
            {
                InterceptionNative.interception_destroy_context(_context);
                _context = IntPtr.Zero;
            }
        }
    }

    private void RemapLoop(CancellationToken ct)
    {
        var stroke = new InterceptionNative.KeyStroke();

        while (!ct.IsCancellationRequested && _context != IntPtr.Zero)
        {
            int device = InterceptionNative.interception_wait_with_timeout(_context, 50);
            if (device <= 0) continue;

            if (InterceptionNative.interception_receive(_context, device, ref stroke, 1) > 0)
            {
                if (!_deviceCache.TryGetValue(device, out var devInfo))
                {
                    string hwId = InterceptionNative.GetHardwareId(_context, device);
                    string friendly = InterceptionNative.GetFriendlyDeviceName(hwId, device);
                    devInfo = (hwId, friendly);
                    _deviceCache[device] = devInfo;
                }

                bool isE0 = (stroke.State & InterceptionNative.INTERCEPTION_KEY_E0) != 0;
                bool isKeyUp = (stroke.State & InterceptionNative.INTERCEPTION_KEY_UP) != 0;
                bool remapped = false;

                foreach (var remap in _config)
                {
                    if (!MatchesKeyboard(remap.Keyboard, device, devInfo.HardwareId, devInfo.FriendlyName))
                        continue;

                    var fromKey = KeyNames.FindKey(remap.From);
                    if (fromKey == null) continue;

                    // Strict match on scan code and E0 flag
                    if (fromKey.Code == stroke.Code && fromKey.IsE0 == isE0)
                    {
                        var toKey = KeyNames.FindKey(remap.To);
                        if (toKey != null)
                        {
                            if (toKey.VirtualKey != 0)
                            {
                                SendInjectedKey(toKey.VirtualKey, (byte)toKey.Code, toKey.IsE0, isKeyUp);
                                if (!isKeyUp)
                                {
                                    OnLog?.Invoke($"[Driver] Remapped [{remap.From}] -> [{remap.To}] (Dev {device})");
                                }
                                remapped = true;
                                break;
                            }
                            else
                            {
                                stroke.Code = toKey.Code;
                                if (toKey.IsE0)
                                {
                                    stroke.State |= InterceptionNative.INTERCEPTION_KEY_E0;
                                }
                                else
                                {
                                    stroke.State &= unchecked((ushort)~InterceptionNative.INTERCEPTION_KEY_E0);
                                }
                                InterceptionNative.interception_send(_context, device, ref stroke, 1);
                                if (!isKeyUp)
                                {
                                    OnLog?.Invoke($"[Driver] Remapped [{remap.From}] -> [{remap.To}] (Dev {device})");
                                }
                                remapped = true;
                                break;
                            }
                        }
                    }
                }

                if (!remapped)
                {
                    InterceptionNative.interception_send(_context, device, ref stroke, 1);
                }
            }
        }
    }

    private IntPtr RemapHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var hookStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);

            // Ignore injected events to prevent cycles / cascade
            if ((hookStruct.flags & LLKHF_INJECTED) != 0 || hookStruct.dwExtraInfo == (UIntPtr)INJECTED_EXTRA_INFO)
            {
                return CallNextHookEx(_hookId, nCode, wParam, lParam);
            }

            bool isKeyDown = (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN);
            bool isKeyUp = (wParam == (IntPtr)WM_KEYUP || wParam == (IntPtr)WM_SYSKEYUP);

            if (isKeyDown || isKeyUp)
            {
                byte vk = (byte)hookStruct.vkCode;
                string keyName = KeyNames.GetKeyNameFromVK(vk);

                // Check if any rule remaps FROM this key
                var fromKeyInfo = KeyNames.FindKey(keyName);
                if (fromKeyInfo != null && fromKeyInfo.IsE0)
                {
                    foreach (var remap in _config)
                    {
                        if (remap.From.Equals(keyName, StringComparison.OrdinalIgnoreCase))
                        {
                            var toKey = KeyNames.FindKey(remap.To);
                            if (toKey != null)
                            {
                                SendInjectedKey(toKey.VirtualKey, (byte)toKey.Code, toKey.IsE0, isKeyUp);
                                // If target is a standard character key, also ensure KeyUp is sent on KeyDown
                                if (isKeyDown && !toKey.IsE0 && toKey.VirtualKey != 0)
                                {
                                    SendInjectedKey(toKey.VirtualKey, (byte)toKey.Code, toKey.IsE0, true);
                                }

                                if (isKeyDown)
                                {
                                    OnLog?.Invoke($"[Hook] Remapped [{remap.From}] -> [{remap.To}]");
                                }
                            }
                            return (IntPtr)1;
                        }
                    }
                }
            }
        }
        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public static void SendInjectedKey(byte vk, byte scan, bool isE0, bool isKeyUp)
    {
        uint flags = 0;
        if (isE0) flags |= KEYEVENTF_EXTENDEDKEY;
        if (isKeyUp) flags |= KEYEVENTF_KEYUP;

        keybd_event(vk, scan, flags, (UIntPtr)INJECTED_EXTRA_INFO);
    }

    public static bool MatchesKeyboard(string configKeyboard, int deviceId, string hardwareId, string friendlyName)
    {
        if (string.IsNullOrWhiteSpace(configKeyboard))
            return true;

        if (configKeyboard.Equals("All", StringComparison.OrdinalIgnoreCase) ||
            configKeyboard.Equals("All Keyboards", StringComparison.OrdinalIgnoreCase))
            return true;

        if (configKeyboard.Equals(friendlyName, StringComparison.OrdinalIgnoreCase))
            return true;

        var devMatch = Regex.Match(configKeyboard, @"\[Dev (\d+)\]", RegexOptions.IgnoreCase);
        if (devMatch.Success && int.TryParse(devMatch.Groups[1].Value, out int targetDevId))
        {
            if (targetDevId == deviceId) return true;
        }

        var vidMatch = Regex.Match(configKeyboard, @"VID_[0-9A-Fa-f]+", RegexOptions.IgnoreCase);
        if (vidMatch.Success && !string.IsNullOrEmpty(hardwareId))
        {
            if (hardwareId.Contains(vidMatch.Value, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        if (!string.IsNullOrEmpty(hardwareId))
        {
            if (hardwareId.Contains(configKeyboard, StringComparison.OrdinalIgnoreCase) ||
                configKeyboard.Contains(hardwareId, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        if (friendlyName.Contains(configKeyboard, StringComparison.OrdinalIgnoreCase) ||
            configKeyboard.Contains(friendlyName, StringComparison.OrdinalIgnoreCase))
            return true;

        if (configKeyboard.Equals($"Keyboard {deviceId}", StringComparison.OrdinalIgnoreCase) ||
            configKeyboard.Equals($"Dev {deviceId}", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}
