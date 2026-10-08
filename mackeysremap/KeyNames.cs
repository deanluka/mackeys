namespace MacKeysRemap;

public class KeyInfo
{
    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public ushort Code { get; set; }
    public bool IsE0 { get; set; }
    public byte VirtualKey { get; set; }

    public KeyInfo(string name, string displayName, ushort code, bool isE0, byte virtualKey = 0)
    {
        Name = name;
        DisplayName = displayName;
        Code = code;
        IsE0 = isE0;
        VirtualKey = virtualKey;
    }
}

public static class KeyNames
{
    public const byte VK_LALT = 0x12;
    public const byte VK_RALT = 0x12;
    public const byte VK_LWIN = 0x5B;
    public const byte VK_RWIN = 0x5C;
    public const byte VK_LCONTROL = 0x11;
    public const byte VK_RCONTROL = 0x11;
    public const byte VK_LSHIFT = 0x10;
    public const byte VK_RSHIFT = 0x10;

    public const byte VK_VOLUME_MUTE = 0xAD;
    public const byte VK_VOLUME_DOWN = 0xAE;
    public const byte VK_VOLUME_UP = 0xAF;
    public const byte VK_MEDIA_NEXT_TRACK = 0xB0;
    public const byte VK_MEDIA_PREV_TRACK = 0xB1;
    public const byte VK_MEDIA_STOP = 0xB2;
    public const byte VK_MEDIA_PLAY_PAUSE = 0xB3;

    private static readonly List<KeyInfo> KeyList = new()
    {
        new("LAlt", "Left Alt (Option)", 0x38, false, VK_LALT),
        new("RAlt", "Right Alt (Option)", 0x38, true, VK_RALT),
        new("LWin", "Left Windows (Command)", 0x5B, true, VK_LWIN),
        new("RWin", "Right Windows (Command)", 0x5C, true, VK_RWIN),
        new("LCtrl", "Left Control", 0x1D, false, VK_LCONTROL),
        new("RCtrl", "Right Control", 0x1D, true, VK_RCONTROL),
        new("LShift", "Left Shift", 0x2A, false, VK_LSHIFT),
        new("RShift", "Right Shift", 0x36, false, VK_RSHIFT),
        new("CapsLock", "Caps Lock", 0x3A, false, 0x14),
        new("Tab", "Tab", 0x0F, false, 0x09),
        new("Enter", "Enter", 0x1C, false, 0x0D),
        new("Backspace", "Backspace", 0x0E, false, 0x08),
        new("Space", "Space", 0x39, false, 0x20),
        new("Escape", "Escape", 0x01, false, 0x1B),
        new("Insert", "Insert", 0x52, true, 0x2D),
        new("Delete", "Delete", 0x53, true, 0x2E),
        new("Home", "Home", 0x47, true, 0x24),
        new("End", "End", 0x4F, true, 0x23),
        new("PageUp", "Page Up", 0x49, true, 0x21),
        new("PageDown", "Page Down", 0x51, true, 0x22),
        new("Up", "Up Arrow", 0x48, true, 0x26),
        new("Down", "Down Arrow", 0x50, true, 0x28),
        new("Left", "Left Arrow", 0x4B, true, 0x25),
        new("Right", "Right Arrow", 0x4D, true, 0x27),
        new("F1", "F1", 0x3B, false, 0x70),
        new("F2", "F2", 0x3C, false, 0x71),
        new("F3", "F3", 0x3D, false, 0x72),
        new("F4", "F4", 0x3E, false, 0x73),
        new("F5", "F5", 0x3F, false, 0x74),
        new("F6", "F6", 0x40, false, 0x75),
        new("F7", "F7", 0x41, false, 0x76),
        new("F8", "F8", 0x42, false, 0x77),
        new("F9", "F9", 0x43, false, 0x78),
        new("F10", "F10", 0x44, false, 0x79),
        new("F11", "F11", 0x57, false, 0x7A),
        new("F12", "F12", 0x58, false, 0x7B),
        new("PrintScreen", "Print Screen", 0x37, true, 0x2C),
        new("ScrollLock", "Scroll Lock", 0x46, false, 0x91),
        new("Pause", "Pause/Break", 0x45, true, 0x13),
        new("NumLock", "Num Lock", 0x45, false, 0x90),
        new("VolumeMute", "Volume Mute", 0x20, true, VK_VOLUME_MUTE),
        new("VolumeDown", "Volume Down", 0x2E, true, VK_VOLUME_DOWN),
        new("VolumeUp", "Volume Up", 0x30, true, VK_VOLUME_UP),
        new("PlayPause", "Play/Pause", 0x22, true, VK_MEDIA_PLAY_PAUSE),
        new("NextTrack", "Next Track", 0x19, true, VK_MEDIA_NEXT_TRACK),
        new("PrevTrack", "Previous Track", 0x10, true, VK_MEDIA_PREV_TRACK),
        new("Stop", "Stop", 0x24, true, VK_MEDIA_STOP),
        new("A", "A", 0x1E, false, (byte)'A'),
        new("B", "B", 0x30, false, (byte)'B'),
        new("C", "C", 0x2E, false, (byte)'C'),
        new("D", "D", 0x20, false, (byte)'D'),
        new("E", "E", 0x12, false, (byte)'E'),
        new("F", "F", 0x21, false, (byte)'F'),
        new("G", "G", 0x22, false, (byte)'G'),
        new("H", "H", 0x23, false, (byte)'H'),
        new("I", "I", 0x17, false, (byte)'I'),
        new("J", "J", 0x24, false, (byte)'J'),
        new("K", "K", 0x25, false, (byte)'K'),
        new("L", "L", 0x26, false, (byte)'L'),
        new("M", "M", 0x32, false, (byte)'M'),
        new("N", "N", 0x31, false, (byte)'N'),
        new("O", "O", 0x18, false, (byte)'O'),
        new("P", "P", 0x19, false, (byte)'P'),
        new("Q", "Q", 0x10, false, (byte)'Q'),
        new("R", "R", 0x13, false, (byte)'R'),
        new("S", "S", 0x1F, false, (byte)'S'),
        new("T", "T", 0x14, false, (byte)'T'),
        new("U", "U", 0x16, false, (byte)'U'),
        new("V", "V", 0x2F, false, (byte)'V'),
        new("W", "W", 0x11, false, (byte)'W'),
        new("X", "X", 0x2D, false, (byte)'X'),
        new("Y", "Y", 0x15, false, (byte)'Y'),
        new("Z", "Z", 0x2C, false, (byte)'Z'),
        new("1", "1", 0x02, false, (byte)'1'),
        new("2", "2", 0x03, false, (byte)'2'),
        new("3", "3", 0x04, false, (byte)'3'),
        new("4", "4", 0x05, false, (byte)'4'),
        new("5", "5", 0x06, false, (byte)'5'),
        new("6", "6", 0x07, false, (byte)'6'),
        new("7", "7", 0x08, false, (byte)'7'),
        new("8", "8", 0x09, false, (byte)'8'),
        new("9", "9", 0x0A, false, (byte)'9'),
        new("0", "0", 0x0B, false, (byte)'0'),
    };

    private static readonly Dictionary<string, KeyInfo> ByName =
        KeyList.ToDictionary(k => k.Name, StringComparer.OrdinalIgnoreCase);

    public static KeyInfo? FindKey(string keyName)
    {
        if (string.IsNullOrWhiteSpace(keyName)) return null;
        if (ByName.TryGetValue(keyName, out var info)) return info;
        return null;
    }

    public static string GetKeyName(ushort code, bool isE0)
    {
        var match = KeyList.FirstOrDefault(k => k.Code == code && k.IsE0 == isE0);
        return match != null ? match.Name : (isE0 ? $"E0_0x{code:X2}" : $"0x{code:X2}");
    }

    public static string GetKeyNameFromVK(byte vk)
    {
        var match = KeyList.FirstOrDefault(k => k.VirtualKey == vk && k.VirtualKey != 0);
        return match != null ? match.Name : $"VK_0x{vk:X2}";
    }

    public static string GetDisplayName(string keyName)
    {
        var key = FindKey(keyName);
        return key != null ? key.DisplayName : keyName;
    }
}
