using System.Text.Json;

namespace MacKeysRemapGui;

public class RemappingConfig
{
    public string InternalKeyboard { get; set; } = "Apple";
    public List<KeyRemapping> Remappings { get; set; } = new();
}

public class KeyRemapping
{
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

    public static RemappingConfig Load()
    {
        if (!File.Exists(ConfigPath))
        {
            var defaultConfig = new RemappingConfig();
            Save(defaultConfig);
            return defaultConfig;
        }

        string json = File.ReadAllText(ConfigPath);
        return JsonSerializer.Deserialize<RemappingConfig>(json) ?? new RemappingConfig();
    }

    public static void Save(RemappingConfig config)
    {
        string json = JsonSerializer.Serialize(config, Options);
        File.WriteAllText(ConfigPath, json);
    }

    public static string GetConfigPath() => ConfigPath;
}
