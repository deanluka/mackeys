using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace MacKeysRemap;

/// <summary>
/// Mac Keys Remapper for Windows (Boot Camp)
/// Remaps internal Mac keyboard: Option→Win, Command→Alt
/// Does NOT affect external Windows keyboards
///
/// Uses Interception driver via P/Invoke (no NuGet package needed).
/// </summary>
class Program
{
    // Interception DLL paths (must be in same folder as exe or in PATH)
    private const string InterceptionDll = "interception.dll";

    // Interception constants
    private const int INTERCEPTION_KEY_DOWN = 0x00;
    private const int INTERCEPTION_KEY_UP = 0x01;
    private const int INTERCEPTION_KEYBOARD = 1;
    private const int INTERCEPTION_MOUSE = 2;
    private const int INTERCEPTION_MAX_KEYBOARD = 1;
    private const int INTERCEPTION_MAX_MOUSE = 2;

    // Filter: process all keyboards
    private const ushort INTERCEPTION_FILTER_KEYBOARD_ALL = 0xFFFF;

    // Key codes (scan codes)
    private const ushort SCANCODE_LALT = 0x38;    // Left Alt
    private const ushort SCANCODE_LWIN = 0x5B;    // Left Windows
    private const ushort SCANCODE_RALT = 0xE038;  // Right Alt (extended)
    private const ushort SCANCODE_RWIN = 0xE05C;  // Right Windows (extended)

    // P/Invoke declarations
    [DllImport(InterceptionDll, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr interception_create_context();

    [DllImport(InterceptionDll, CallingConvention = CallingConvention.Cdecl)]
    private static extern void interception_destroy_context(IntPtr context);

    [DllImport(InterceptionDll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int interception_get(IntPtr context, int device);

    [DllImport(InterceptionDll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int interception_send(IntPtr context, int device, ref KeyStroke stroke, int n);

    [DllImport(InterceptionDll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int interception_receive(IntPtr context, int device, ref KeyStroke stroke, int n);

    [DllImport(InterceptionDll, CallingConvention = CallingConvention.Cdecl)]
    private static extern void interception_set_filter(IntPtr context, int predicate, ushort filter);

    [DllImport(InterceptionDll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int interception_is_keyboard(int device);

    [DllImport(InterceptionDll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int interception_is_mouse(int device);

    [DllImport(InterceptionDll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int interception_get_hardware_id(int device, IntPtr buffer, int size);

    // Key stroke structure
    [StructLayout(LayoutKind.Sequential)]
    private struct KeyStroke
    {
        public ushort Code;
        public ushort State;
        public uint Information;
    }

    // Device info
    private class KeyboardDevice
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    private static IntPtr _context;
    private static CancellationTokenSource? _cts;
    private static readonly List<KeyboardDevice> Keyboards = new();
    private static int _internalKeyboardId = -1;
    private static string _internalKeyboardName = "Apple";

    // Remapping: Option→Win, Command→Alt (scan codes)
    private static readonly Dictionary<ushort, ushort> RemapTable = new()
    {
        { SCANCODE_LALT, SCANCODE_LWIN },   // Left Alt → Left Win
        { SCANCODE_LWIN, SCANCODE_LALT },   // Left Win → Left Alt
    };

    static void Main(string[] args)
    {
        Console.WriteLine("========================================");
        Console.WriteLine("  Mac Keys Remapper for Windows");
        Console.WriteLine("  Boot Camp Edition");
        Console.WriteLine("========================================");
        Console.WriteLine();

        if (!IsAdministrator())
        {
            Console.WriteLine("ERROR: This application requires administrator privileges.");
            Console.WriteLine("Please run as Administrator.");
            Console.WriteLine();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
            return;
        }

        if (args.Length > 0)
        {
            _internalKeyboardName = args[0];
        }

        Console.WriteLine($"Looking for internal keyboard matching: {_internalKeyboardName}");
        Console.WriteLine();

        // Create Interception context
        _context = interception_create_context();
        if (_context == IntPtr.Zero)
        {
            Console.WriteLine("ERROR: Failed to create Interception context.");
            Console.WriteLine();
            Console.WriteLine("Please make sure the Interception driver is installed:");
            Console.WriteLine("1. Download from https://github.com/oblitum/Interception");
            Console.WriteLine("2. Run install-interception.exe /install");
            Console.WriteLine("3. Reboot your computer");
            Console.WriteLine();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
            return;
        }

        // Set filter to process all keyboards
        interception_set_filter(_context, INTERCEPTION_KEYBOARD, INTERCEPTION_FILTER_KEYBOARD_ALL);

        // Enumerate keyboards
        EnumerateKeyboards();

        if (Keyboards.Count == 0)
        {
            Console.WriteLine("ERROR: No keyboards found.");
            interception_destroy_context(_context);
            Console.ReadKey();
            return;
        }

        // Find internal Mac keyboard
        var internalKeyboard = Keyboards.FirstOrDefault(k =>
            k.Name.Contains(_internalKeyboardName, StringComparison.OrdinalIgnoreCase));

        if (internalKeyboard == null)
        {
            Console.WriteLine($"WARNING: Could not find internal keyboard matching '{_internalKeyboardName}'.");
            Console.WriteLine("Available keyboards:");
            foreach (var kb in Keyboards)
            {
                Console.WriteLine($"  - {kb.Name} (ID: {kb.Id})");
            }
            Console.WriteLine();
            Console.WriteLine("Please specify the correct keyboard name as a command line argument.");
            Console.WriteLine("Example: MacKeysRemap.exe \"Apple Internal Keyboard\"");
            Console.WriteLine();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
            interception_destroy_context(_context);
            return;
        }

        _internalKeyboardId = internalKeyboard.Id;
        Console.WriteLine($"Internal keyboard found: {internalKeyboard.Name} (ID: {internalKeyboard.Id})");
        Console.WriteLine();

        Console.WriteLine("Remapping rules for internal keyboard:");
        Console.WriteLine("  Option (Alt) → Win");
        Console.WriteLine("  Command (Win) → Alt");
        Console.WriteLine();
        Console.WriteLine("External keyboards: no remapping");
        Console.WriteLine();
        Console.WriteLine("Press Ctrl+C to exit.");
        Console.WriteLine();

        _cts = new CancellationTokenSource();
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            _cts.Cancel();
        };

        try
        {
            RunRemapLoop(_cts.Token);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine();
            Console.WriteLine("Shutting down...");
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.WriteLine($"ERROR: {ex.Message}");
        }

        interception_destroy_context(_context);
        Console.WriteLine("Done.");
    }

    static void EnumerateKeyboards()
    {
        for (int i = 1; i <= INTERCEPTION_MAX_KEYBOARD; i++)
        {
            if (interception_is_keyboard(i) == 1)
            {
                // Get hardware ID
                var buffer = Marshal.AllocHGlobal(1024);
                try
                {
                    int len = interception_get_hardware_id(i, buffer, 1024);
                    string name = len > 0 ? Marshal.PtrToStringAnsi(buffer) ?? $"Keyboard {i}" : $"Keyboard {i}";
                    Keyboards.Add(new KeyboardDevice { Id = i, Name = name });
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
        }
    }

    static void RunRemapLoop(CancellationToken ct)
    {
        var stroke = new KeyStroke();

        while (!ct.IsCancellationRequested)
        {
            // Try reading from any keyboard
            for (int i = 1; i <= INTERCEPTION_MAX_KEYBOARD; i++)
            {
                if (interception_is_keyboard(i) != 1) continue;

                int result = interception_receive(_context, i, ref stroke, 1);
                if (result == 0) continue;

                // Check if this is from the internal Mac keyboard
                bool isInternal = (i == _internalKeyboardId);

                if (isInternal && RemapTable.TryGetValue(stroke.Code, out var newCode))
                {
                    // Remap the key
                    stroke.Code = newCode;
                }

                // Send the (possibly remapped) stroke
                interception_send(_context, i, ref stroke, 1);
            }

            Thread.Sleep(1);
        }
    }

    static bool IsAdministrator()
    {
        var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        var principal = new System.Security.Principal.WindowsPrincipal(identity);
        return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
    }
}
