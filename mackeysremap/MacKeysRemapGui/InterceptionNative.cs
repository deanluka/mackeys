using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace MacKeysRemapGui;

public static class InterceptionNative
{
    public const string InterceptionDll = "interception.dll";
    public const int INTERCEPTION_MAX_KEYBOARD = 10;
    public const int INTERCEPTION_MAX_MOUSE = 10;
    public const int INTERCEPTION_MAX_DEVICE = INTERCEPTION_MAX_KEYBOARD + INTERCEPTION_MAX_MOUSE;

    public const ushort INTERCEPTION_FILTER_KEY_NONE = 0x0000;
    public const ushort INTERCEPTION_FILTER_KEY_ALL = 0xFFFF;
    public const ushort INTERCEPTION_FILTER_KEY_DOWN = 0x01;
    public const ushort INTERCEPTION_FILTER_KEY_UP = 0x02;
    public const ushort INTERCEPTION_FILTER_KEY_E0 = 0x04;
    public const ushort INTERCEPTION_FILTER_KEY_E1 = 0x08;

    public const ushort INTERCEPTION_KEY_DOWN = 0x00;
    public const ushort INTERCEPTION_KEY_UP = 0x01;
    public const ushort INTERCEPTION_KEY_E0 = 0x02;
    public const ushort INTERCEPTION_KEY_E1 = 0x04;

    public const uint INJECTED_EXTRA_INFO = 0xA55C1E00;

    public const int WH_KEYBOARD_LL = 13;
    public const int WM_KEYDOWN = 0x0100;
    public const int WM_KEYUP = 0x0101;
    public const int WM_SYSKEYDOWN = 0x0104;
    public const int WM_SYSKEYUP = 0x0105;
    public const uint LLKHF_INJECTED = 0x00000010;

    public const uint KEYEVENTF_KEYUP = 0x0002;
    public const uint KEYEVENTF_SCANCODE = 0x0008;
    public const uint KEYEVENTF_EXTENDEDKEY = 0x0001;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int InterceptionPredicate(int device);

    [StructLayout(LayoutKind.Sequential)]
    public struct KeyStroke
    {
        public ushort Code;
        public ushort State;
        public uint Information;
    }

    [DllImport(InterceptionDll, EntryPoint = "interception_create_context", CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr interception_create_context();

    [DllImport(InterceptionDll, EntryPoint = "interception_destroy_context", CallingConvention = CallingConvention.Cdecl)]
    public static extern void interception_destroy_context(IntPtr context);

    [DllImport(InterceptionDll, EntryPoint = "interception_set_filter", CallingConvention = CallingConvention.Cdecl)]
    public static extern void interception_set_filter(IntPtr context, InterceptionPredicate predicate, ushort filter);

    [DllImport(InterceptionDll, EntryPoint = "interception_get_filter", CallingConvention = CallingConvention.Cdecl)]
    public static extern ushort interception_get_filter(IntPtr context, int device);

    [DllImport(InterceptionDll, EntryPoint = "interception_wait", CallingConvention = CallingConvention.Cdecl)]
    public static extern int interception_wait(IntPtr context);

    [DllImport(InterceptionDll, EntryPoint = "interception_wait_with_timeout", CallingConvention = CallingConvention.Cdecl)]
    public static extern int interception_wait_with_timeout(IntPtr context, uint milliseconds);

    [DllImport(InterceptionDll, EntryPoint = "interception_receive", CallingConvention = CallingConvention.Cdecl)]
    public static extern int interception_receive(IntPtr context, int device, ref KeyStroke stroke, uint nstroke);

    [DllImport(InterceptionDll, EntryPoint = "interception_send", CallingConvention = CallingConvention.Cdecl)]
    public static extern int interception_send(IntPtr context, int device, ref KeyStroke stroke, uint nstroke);

    [DllImport(InterceptionDll, EntryPoint = "interception_is_keyboard", CallingConvention = CallingConvention.Cdecl)]
    public static extern int interception_is_keyboard(int device);

    [DllImport(InterceptionDll, EntryPoint = "interception_is_mouse", CallingConvention = CallingConvention.Cdecl)]
    public static extern int interception_is_mouse(int device);

    [DllImport(InterceptionDll, EntryPoint = "interception_is_invalid", CallingConvention = CallingConvention.Cdecl)]
    public static extern int interception_is_invalid(int device);

    [DllImport(InterceptionDll, EntryPoint = "interception_get_hardware_id", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint interception_get_hardware_id(IntPtr context, int device, IntPtr hardwareIdBuffer, uint bufferSize);

    // Static delegate to avoid garbage collection during native callback invocations
    public static readonly InterceptionPredicate IsKeyboardPredicate = (device) => interception_is_keyboard(device);

    [StructLayout(LayoutKind.Sequential)]
    public struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    public static string GetHardwareId(IntPtr context, int deviceId)
    {
        if (context == IntPtr.Zero || interception_is_keyboard(deviceId) != 1)
            return string.Empty;

        const int maxChars = 512;
        IntPtr buffer = Marshal.AllocHGlobal(maxChars * sizeof(char));
        try
        {
            uint bytesCopied = interception_get_hardware_id(context, deviceId, buffer, (uint)(maxChars * sizeof(char)));
            if (bytesCopied > 0)
            {
                string? id = Marshal.PtrToStringUni(buffer);
                return id?.Trim() ?? string.Empty;
            }
            return string.Empty;
        }
        catch
        {
            return string.Empty;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public static string GetFriendlyDeviceName(string hardwareId, int deviceId)
    {
        if (string.IsNullOrWhiteSpace(hardwareId))
            return $"Keyboard {deviceId}";

        string brand;
        if (hardwareId.Contains("VID_05AC", StringComparison.OrdinalIgnoreCase))
            brand = "Apple Keyboard";
        else if (hardwareId.Contains("VID_046D", StringComparison.OrdinalIgnoreCase))
            brand = "Logitech Keyboard";
        else if (hardwareId.Contains("VID_045E", StringComparison.OrdinalIgnoreCase))
            brand = "Microsoft Keyboard";
        else if (hardwareId.Contains("VID_1532", StringComparison.OrdinalIgnoreCase))
            brand = "Razer Keyboard";
        else if (hardwareId.Contains("VID_1B1C", StringComparison.OrdinalIgnoreCase))
            brand = "Corsair Keyboard";
        else if (hardwareId.Contains("VID_0B05", StringComparison.OrdinalIgnoreCase))
            brand = "ASUS Keyboard";
        else if (hardwareId.Contains("VID_17EF", StringComparison.OrdinalIgnoreCase))
            brand = "Lenovo Keyboard";
        else if (hardwareId.Contains("VID_413C", StringComparison.OrdinalIgnoreCase))
            brand = "Dell Keyboard";
        else if (hardwareId.Contains("VID_03F0", StringComparison.OrdinalIgnoreCase))
            brand = "HP Keyboard";
        else if (hardwareId.Contains("VID_3434", StringComparison.OrdinalIgnoreCase) || hardwareId.Contains("Keychron", StringComparison.OrdinalIgnoreCase))
            brand = "Keychron Keyboard";
        else if (hardwareId.Contains("ACPI") || hardwareId.Contains("PNP0303", StringComparison.OrdinalIgnoreCase))
            brand = "Internal / PS2 Keyboard";
        else if (hardwareId.StartsWith("HID", StringComparison.OrdinalIgnoreCase))
            brand = "USB/HID Keyboard";
        else
            brand = "Keyboard";

        string shortId = "";
        var match = Regex.Match(hardwareId, @"(VID_[0-9A-Fa-f]+&PID_[0-9A-Fa-f]+)", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            shortId = $" ({match.Value})";
        }

        return $"{brand}{shortId} [Dev {deviceId}]";
    }
}
