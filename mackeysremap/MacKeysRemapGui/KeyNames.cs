namespace MacKeysRemapGui;

public static class KeyNames
{
    public static readonly Dictionary<string, string> Keys = new()
    {
        // Modifier keys
        { "LAlt", "Left Alt (Option)" },
        { "RAlt", "Right Alt (Option)" },
        { "LWin", "Left Windows (Command)" },
        { "RWin", "Right Windows (Command)" },
        { "LCtrl", "Left Control" },
        { "RCtrl", "Right Control" },
        { "LShift", "Left Shift" },
        { "RShift", "Right Shift" },

        // Function keys
        { "F1", "F1" },
        { "F2", "F2" },
        { "F3", "F3" },
        { "F4", "F4" },
        { "F5", "F5" },
        { "F6", "F6" },
        { "F7", "F7" },
        { "F8", "F8" },
        { "F9", "F9" },
        { "F10", "F10" },
        { "F11", "F11" },
        { "F12", "F12" },

        // Navigation
        { "Insert", "Insert" },
        { "Delete", "Delete" },
        { "Home", "Home" },
        { "End", "End" },
        { "PageUp", "Page Up" },
        { "PageDown", "Page Down" },

        // Media keys
        { "VolumeUp", "Volume Up" },
        { "VolumeDown", "Volume Down" },
        { "VolumeMute", "Volume Mute" },
        { "PlayPause", "Play/Pause" },
        { "NextTrack", "Next Track" },
        { "PrevTrack", "Previous Track" },
        { "Stop", "Stop" },
        { "MediaSelect", "Media Select" },

        // Other
        { "PrintScreen", "Print Screen" },
        { "ScrollLock", "Scroll Lock" },
        { "Pause", "Pause/Break" },
        { "CapsLock", "Caps Lock" },
        { "NumLock", "Num Lock" },
        { "Escape", "Escape" },
        { "Space", "Space" },
        { "Tab", "Tab" },
        { "Enter", "Enter" },
        { "Backspace", "Backspace" },
    };

    public static string GetDisplayName(string key)
    {
        return Keys.TryGetValue(key, out var name) ? name : key;
    }

    public static List<string> GetAllKeyNames() => Keys.Keys.ToList();
}
