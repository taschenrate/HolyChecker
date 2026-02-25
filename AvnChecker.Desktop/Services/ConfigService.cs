using System.Text.Json;
using AvnChecker.Desktop.Models;

namespace AvnChecker.Desktop.Services;

public sealed class ConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true
    };

    private readonly string _configPath;

    public ConfigService(string baseDirectory)
    {
        _configPath = Path.Combine(baseDirectory, "appsettings.json");
    }

    public AppConfig Load()
    {
        if (!File.Exists(_configPath))
        {
            var defaultConfig = AppConfig.CreateDefault();
            Save(defaultConfig);
            return defaultConfig;
        }

        try
        {
            var json = File.ReadAllText(_configPath);
            var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? AppConfig.CreateDefault();
            if (config.Tools.Count == 0)
            {
                config.Tools = AppConfig.CreateDefault().Tools;
            }

            return config;
        }
        catch
        {
            var fallback = AppConfig.CreateDefault();
            Save(fallback);
            return fallback;
        }
    }

    public void Save(AppConfig config)
    {
        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(_configPath, json);
    }

    public void Export(AppConfig config, string destinationPath)
    {
        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(destinationPath, json);
    }

    public AppConfig ResetToDefault()
    {
        var config = AppConfig.CreateDefault();
        Save(config);
        return config;
    }
}
