using System.Runtime.InteropServices;

namespace MacKeysRemapGui;

public class InterceptionRemapper
{
    private const string InterceptionDll = "interception.dll";
    private const int INTERCEPTION_KEYBOARD = 1;
    private const int INTERCEPTION_MAX_KEYBOARD = 1;
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
    private static extern void interception_set_filter(IntPtr context, int predicate, ushort filter);

    [DllImport(InterceptionDll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int interception_is_keyboard(int device);

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyStroke
    {
        public ushort Code;
        public ushort State;
        public uint Information;
    }

    private readonly RemappingConfig _config;
    private IntPtr _context;
    private CancellationTokenSource? _cts;
    private Task? _remapTask;

    public InterceptionRemapper(RemappingConfig config)
    {
        _config = config;
    }

    public bool Start()
    {
        _context = interception_create_context();
        if (_context == IntPtr.Zero) return false;

        interception_set_filter(_context, INTERCEPTION_KEYBOARD, INTERCEPTION_FILTER_KEYBOARD_ALL);

        _cts = new CancellationTokenSource();
        _remapTask = Task.Run(() => RemapLoop(_cts.Token));
        return true;
    }

    public void Stop()
    {
        _cts?.Cancel();
        _remapTask?.Wait(1000);
        if (_context != IntPtr.Zero)
        {
            interception_destroy_context(_context);
            _context = IntPtr.Zero;
        }
    }

    private void RemapLoop(CancellationToken ct)
    {
        var stroke = new KeyStroke();

        while (!ct.IsCancellationRequested)
        {
            for (int i = 1; i <= INTERCEPTION_MAX_KEYBOARD; i++)
            {
                if (interception_is_keyboard(i) != 1) continue;

                int result = interception_receive(_context, i, ref stroke, 1);
                if (result == 0) continue;

                // Check if this is the target keyboard
                // Note: In a real implementation, you'd check the device ID
                // For now, we apply remapping to the selected keyboard

                // Apply remapping
                foreach (var remap in _config.Remappings)
                {
                    if (GetScanCode(remap.From) == stroke.Code)
                    {
                        stroke.Code = GetScanCode(remap.To);
                        break;
                    }
                }

                interception_send(_context, i, ref stroke, 1);
            }

            Thread.Sleep(1);
        }
    }

    private static ushort GetScanCode(string keyName)
    {
        return keyName switch
        {
            "LAlt" => 0x38,
            "RAlt" => 0xE038,
            "LWin" => 0x5B,
            "RWin" => 0xE05C,
            "LCtrl" => 0x1D,
            "RCtrl" => 0xE01D,
            "LShift" => 0x2A,
            "RShift" => 0x36,
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
            "Insert" => 0xE052,
            "Delete" => 0xE053,
            "Home" => 0xE047,
            "End" => 0xE04F,
            "PageUp" => 0xE049,
            "PageDown" => 0xE051,
            "PrintScreen" => 0xE037,
            "ScrollLock" => 0x46,
            "Pause" => 0xE045,
            "CapsLock" => 0x3A,
            "NumLock" => 0x45,
            "Escape" => 0x01,
            "Space" => 0x39,
            "Tab" => 0x0F,
            "Enter" => 0x1C,
            "Backspace" => 0x0E,
            _ => 0x00
        };
    }
}
