using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace MacKeysRemapGui;

public class InterceptionRemapper : IDisposable
{
    public event Action<string>? OnLog;

    private const bool EnableVerboseDebugLogging = true;
    private const bool EnableEveryStrokeRawLogging = true;

    private readonly List<KeyRemapping> _config;
    private IntPtr _context = IntPtr.Zero;
    private CancellationTokenSource? _cts;
    private Task? _remapTask;
    private readonly Dictionary<int, (string HardwareId, string FriendlyName)> _deviceCache = new();
    private bool _disposed = false;

    // Shared variable: tracks which device sent the last key (for media key remapping)
    public static string LastActiveDevice = "";

    private IntPtr _hookId = IntPtr.Zero;
    private InterceptionNative.LowLevelKeyboardProc? _hookProc;
    private long _totalStrokesSeen;
    private long _heartbeatLastLogged;

    public InterceptionRemapper(List<KeyRemapping> config)
    {
        _config = config;
    }

    public bool Start()
    {
        _context = InterceptionNative.interception_create_context();
        if (_context == IntPtr.Zero) return false;

        // Set filter on all keyboard devices so we receive every keystroke
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
                OnLog?.Invoke($"Keyboard {i}: {friendly} ({hwId})");
            }
        }

        OnLog?.Invoke($"Installing WH_KEYBOARD_LL hook for media/HID-consumer keys (Interception cannot see those)");
        _hookProc = HookCallback;
        _hookId = InterceptionNative.SetWindowsHookEx(
            InterceptionNative.WH_KEYBOARD_LL,
            _hookProc,
            InterceptionNative.GetModuleHandle(null),
            0);
        if (_hookId == IntPtr.Zero)
        {
            OnLog?.Invoke($"WARNING: Failed to install low-level keyboard hook (error {Marshal.GetLastWin32Error()}). Media keys will not be remapped.");
        }
        else
        {
            OnLog?.Invoke($"Low-level keyboard hook installed successfully.");
        }

        _totalStrokesSeen = 0;
        _heartbeatLastLogged = 0;

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
                InterceptionNative.UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
                OnLog?.Invoke("Low-level keyboard hook uninstalled.");
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
        OnLog?.Invoke($"Interception remap loop started. (VerboseDebugLogging={EnableVerboseDebugLogging}, EveryStrokeRawLogging={EnableEveryStrokeRawLogging}) Waiting for keyboard events...");

        while (!ct.IsCancellationRequested && _context != IntPtr.Zero)
        {
            int device = InterceptionNative.interception_wait_with_timeout(_context, 50);
            if (device <= 0) continue;

            if (InterceptionNative.interception_receive(_context, device, ref stroke, 1) <= 0)
                continue;

            _totalStrokesSeen++;
            if (_totalStrokesSeen - _heartbeatLastLogged >= 500)
            {
                _heartbeatLastLogged = _totalStrokesSeen;
                OnLog?.Invoke($"[Heartbeat] Interception loop alive: {_totalStrokesSeen} total strokes received so far.");
            }

            if (!_deviceCache.TryGetValue(device, out var devInfo))
            {
                string hwId = InterceptionNative.GetHardwareId(_context, device);
                string friendly = InterceptionNative.GetFriendlyDeviceName(hwId, device);
                devInfo = (hwId, friendly);
                _deviceCache[device] = devInfo;
                OnLog?.Invoke($"[NEW DEVICE CACHED] Dev {device}: Friendly='{devInfo.FriendlyName}', HWID='{devInfo.HardwareId}'");
            }

            // Track last active device for media key remapping
            LastActiveDevice = devInfo.FriendlyName;

            bool isE0 = (stroke.State & InterceptionNative.INTERCEPTION_KEY_E0) != 0;
            bool isE1 = (stroke.State & InterceptionNative.INTERCEPTION_KEY_E1) != 0;
            bool isKeyUp = (stroke.State & InterceptionNative.INTERCEPTION_KEY_UP) != 0;

            if (stroke.Information == InterceptionNative.INJECTED_EXTRA_INFO)
            {
                if (!isKeyUp && EnableVerboseDebugLogging)
                {
                    string keyName = KeyNames.GetKeyName(stroke.Code, isE0);
                    OnLog?.Invoke($"[INT][Dev {device}][INJECTED-SKIP] Key={keyName}, Code=0x{stroke.Code:X2}, State=0x{stroke.State:X4} (UP={isKeyUp}, E0={isE0}, E1={isE1}), Info=0x{stroke.Information:X8} -> forwarded untouched (loop guard)");
                }
                InterceptionNative.interception_send(_context, device, ref stroke, 1);
                continue;
            }

            bool matched = false;
            foreach (var remap in _config)
            {
                if (!MatchesKeyboard(remap.Keyboard, device, devInfo.HardwareId, devInfo.FriendlyName))
                    continue;

                var fromKey = KeyNames.FindKey(remap.From);
                if (fromKey == null) continue;

                if (fromKey.Code != stroke.Code || fromKey.IsE0 != isE0)
                    continue;

                var toKey = KeyNames.FindKey(remap.To);
                if (toKey == null) continue;

                ushort oldCode = stroke.Code;

                stroke.Code = toKey.Code;

                if (toKey.IsE0)
                    stroke.State |= InterceptionNative.INTERCEPTION_KEY_E0;
                else
                    stroke.State &= unchecked((ushort)~InterceptionNative.INTERCEPTION_KEY_E0);

                if (!isKeyUp)
                {
                    OnLog?.Invoke($"[INT][Dev {device}][MATCH] Device='{devInfo.FriendlyName}' | ConfigKeyboard='{remap.Keyboard}' | {remap.From} (0x{oldCode:X2},E0={isE0}) -> {remap.To} (0x{stroke.Code:X2},E0={toKey.IsE0}) | RawState=0x{stroke.State:X4} Info=0x{stroke.Information:X8}");
                }

                matched = true;
                break;
            }

            if (!isKeyUp && EnableEveryStrokeRawLogging)
            {
                string keyName = KeyNames.GetKeyName(stroke.Code, isE0);
                string matchTag = matched ? "MATCHED-LOGGED-ABOVE" : "NO-MATCH";
                OnLog?.Invoke($"[INT][Dev {device}][{matchTag}] Device='{devInfo.FriendlyName}' (HWID='{devInfo.HardwareId}') | Key='{keyName}' | Code=0x{stroke.Code:X2} | State=0x{stroke.State:X4} (UP=False, E0={isE0}, E1={isE1}) | Info=0x{stroke.Information:X8}");
            }

            InterceptionNative.interception_send(_context, device, ref stroke, 1);
        }
        OnLog?.Invoke("Interception remap loop exited.");
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0 || lParam == IntPtr.Zero)
            return InterceptionNative.CallNextHookEx(_hookId, nCode, wParam, lParam);

        int msg = wParam.ToInt32();
        bool isDown = msg == InterceptionNative.WM_KEYDOWN || msg == InterceptionNative.WM_SYSKEYDOWN;
        bool isUp = msg == InterceptionNative.WM_KEYUP || msg == InterceptionNative.WM_SYSKEYUP;

        string msgName = msg switch
        {
            0x0100 => "WM_KEYDOWN",
            0x0101 => "WM_KEYUP",
            0x0104 => "WM_SYSKEYDOWN",
            0x0105 => "WM_SYSKEYUP",
            _ => $"MSG_0x{msg:X4}"
        };

        var kbData = Marshal.PtrToStructure<InterceptionNative.KBDLLHOOKSTRUCT>(lParam);
        bool flagInjected = (kbData.flags & InterceptionNative.LLKHF_INJECTED) != 0;
        bool flagExtended = (kbData.flags & 0x00000001) != 0;
        bool flagAltDown = (kbData.flags & 0x00000020) != 0;
        bool flagLowUp = (kbData.flags & 0x00000080) != 0;
        bool hasMarkerExtra = kbData.dwExtraInfo == (UIntPtr)InterceptionNative.INJECTED_EXTRA_INFO;

        string fromKeyNameByVK = KeyNames.GetKeyNameFromVK((byte)kbData.vkCode);
        bool isMediaVK = kbData.vkCode >= 0xAD && kbData.vkCode <= 0xB7;

        // Hook only handles media keys — pass through everything else immediately
        if (!isMediaVK)
            return InterceptionNative.CallNextHookEx(_hookId, nCode, wParam, lParam);

        if (isDown && EnableVerboseDebugLogging)
        {
            string keyLabel = string.IsNullOrEmpty(fromKeyNameByVK) ? $"VK_0x{kbData.vkCode:X2}" : fromKeyNameByVK;
            string mediaTag = isMediaVK ? " **MEDIA/HID-CONSUMER RANGE**" : "";
            OnLog?.Invoke($"[LL]{mediaTag} {msgName} | nCode={nCode} | Key='{keyLabel}' | VK=0x{kbData.vkCode:X2} | Scan=0x{kbData.scanCode:X2} | Flags=0x{kbData.flags:X8} (INJECTED={flagInjected}, EXTENDED={flagExtended}, ALTDOWN={flagAltDown}, UP={flagLowUp}) | ExtraInfo=0x{kbData.dwExtraInfo.ToUInt64():X16} (OUR-MARKER={hasMarkerExtra})");
        }

        if (!isDown && !isUp)
            return InterceptionNative.CallNextHookEx(_hookId, nCode, wParam, lParam);

        if (kbData.dwExtraInfo == (UIntPtr)InterceptionNative.INJECTED_EXTRA_INFO)
        {
            if (isDown && EnableVerboseDebugLogging)
            {
                string keyLabel = string.IsNullOrEmpty(fromKeyNameByVK) ? $"VK_0x{kbData.vkCode:X2}" : fromKeyNameByVK;
                OnLog?.Invoke($"[LL-SKIP] OUR-MARKER in ExtraInfo set -> not remapping Key='{keyLabel}' VK=0x{kbData.vkCode:X2} (synthetic injections from this app to prevent loop)");
            }
            return InterceptionNative.CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        if ((kbData.flags & InterceptionNative.LLKHF_INJECTED) != 0)
        {
            if (isDown && isMediaVK && EnableVerboseDebugLogging)
            {
                string keyLabel = string.IsNullOrEmpty(fromKeyNameByVK) ? $"VK_0x{kbData.vkCode:X2}" : fromKeyNameByVK;
                OnLog?.Invoke($"[LL-OBSERVE] MEDIA key '{keyLabel}' has LLKHF_INJECTED flag (from Logitech/HID driver — normal). Will still evaluate rules; only OUR-MARKER=0x{InterceptionNative.INJECTED_EXTRA_INFO:X8} blocks remap.");
            }
        }

        if (string.IsNullOrEmpty(fromKeyNameByVK))
        {
            if (isDown)
                OnLog?.Invoke($"[LL-SKIP] No internal name for VK=0x{kbData.vkCode:X2} -> passing through");
            return InterceptionNative.CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        bool matched = false;
        foreach (var remap in _config)
        {
            bool keyboardIsGlobal =
                string.Equals(remap.Keyboard, "All", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(remap.Keyboard, "All Keyboards", StringComparison.OrdinalIgnoreCase);

            // For ALL keys (not just media), check if LastActiveDevice matches the rule's scope
            if (!keyboardIsGlobal)
            {
                bool deviceMatches = MatchesKeyboard(remap.Keyboard, 0, "", LastActiveDevice);
                if (!deviceMatches)
                {
                    if (isDown && EnableVerboseDebugLogging)
                    {
                        OnLog?.Invoke($"[LL-SKIP] Rule '{remap.Keyboard}' {remap.From}->{remap.To} — LastActiveDevice='{LastActiveDevice}' does not match scope. Passing through.");
                    }
                    continue;
                }
                if (isDown && EnableVerboseDebugLogging)
                {
                    OnLog?.Invoke($"[LL-MATCH] Rule '{remap.Keyboard}' {remap.From}->{remap.To} — LastActiveDevice='{LastActiveDevice}' matches scope.");
                }
            }

            if (!string.Equals(remap.From, fromKeyNameByVK, StringComparison.OrdinalIgnoreCase))
                continue;

            var toKey = KeyNames.FindKey(remap.To);
            if (toKey == null || toKey.VirtualKey == 0)
            {
                if (isDown)
                    OnLog?.Invoke($"[LL-SKIP] Rule {remap.From}->{remap.To} has no VK mapping for destination -> skipped");
                continue;
            }

            uint flags = 0;
            if (isUp)
                flags |= InterceptionNative.KEYEVENTF_KEYUP;
            if (toKey.IsE0)
                flags |= InterceptionNative.KEYEVENTF_EXTENDEDKEY;

            InterceptionNative.keybd_event(
                (byte)toKey.VirtualKey,
                (byte)toKey.Code,
                flags,
                (UIntPtr)InterceptionNative.INJECTED_EXTRA_INFO);

            if (isDown)
            {
                string scopeTag = keyboardIsGlobal ? "[SCOPE=All-Keyboards]" : $"[SCOPE='{remap.Keyboard}'=matched-anyway-media-no-deviceinfo]";
                OnLog?.Invoke($"[LL-MATCH] {scopeTag} | {fromKeyNameByVK} (VK 0x{kbData.vkCode:X2}) -> {remap.To} (VK 0x{toKey.VirtualKey:X2}) via keybd_event(bScan=0x{toKey.Code:X2}, flags=0x{flags:X4}, ExtraInfo=0x{InterceptionNative.INJECTED_EXTRA_INFO:X8}) | ORIGINAL SWALLOWED");
            }

            matched = true;
            return (IntPtr)1;
        }

        if (!matched && isDown && isMediaVK)
        {
            OnLog?.Invoke($"[LL-NO-MATCH] MEDIA KEY '{fromKeyNameByVK}' (VK=0x{kbData.vkCode:X2}) seen but NO 'All Keyboards' rule references it. Add a rule!");
        }

        return InterceptionNative.CallNextHookEx(_hookId, nCode, wParam, lParam);
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

        // Match by [Dev X] tag in name
        var devMatch = Regex.Match(configKeyboard, @"\[Dev (\d+)\]", RegexOptions.IgnoreCase);
        if (devMatch.Success && int.TryParse(devMatch.Groups[1].Value, out int targetDevId))
        {
            if (targetDevId == deviceId) return true;
        }

        // Match by VID in hardware ID
        var vidMatch = Regex.Match(configKeyboard, @"VID_[0-9A-Fa-f]+", RegexOptions.IgnoreCase);
        if (vidMatch.Success && !string.IsNullOrEmpty(hardwareId))
        {
            if (hardwareId.Contains(vidMatch.Value, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        // Substring match on hardware ID or friendly name
        if (!string.IsNullOrEmpty(hardwareId) &&
            (hardwareId.Contains(configKeyboard, StringComparison.OrdinalIgnoreCase) ||
             configKeyboard.Contains(hardwareId, StringComparison.OrdinalIgnoreCase)))
            return true;

        if (friendlyName.Contains(configKeyboard, StringComparison.OrdinalIgnoreCase) ||
            configKeyboard.Contains(friendlyName, StringComparison.OrdinalIgnoreCase))
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
