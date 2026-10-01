using System.Text.Json;

namespace MacKeysRemapGui;

public class KeyRemapping
{
    public string Keyboard { get; set; } = "";
    public string From { get; set; } = "";
    public string To { get; set; } = "";
}

public static class ConfigManager
{
    private static readonly string ConfigPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "config.json");

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true
    };

    public static List<KeyRemapping> Load()
    {
        if (!File.Exists(ConfigPath))
        {
            var defaultConfig = new List<KeyRemapping>
            {
                new() { Keyboard = "Apple", From = "LAlt", To = "LWin" },
                new() { Keyboard = "Apple", From = "LWin", To = "LAlt" }
            };
            Save(defaultConfig);
            return defaultConfig;
        }

        string json = File.ReadAllText(ConfigPath);
        return JsonSerializer.Deserialize<List<KeyRemapping>>(json) ?? new List<KeyRemapping>();
    }

    public static void Save(List<KeyRemapping> config)
    {
        string json = JsonSerializer.Serialize(config, Options);
        File.WriteAllText(ConfigPath, json);
    }

    public static string GetConfigPath() => ConfigPath;
}
