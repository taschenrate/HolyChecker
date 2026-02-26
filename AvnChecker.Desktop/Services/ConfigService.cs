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
            var updated = EnsureDefaultTools(config);
            updated |= EnsureDefaultModsRisk(config);
            updated |= EnsureDefaultModsScan(config);
            if (updated)
            {
                Save(config);
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

    private static bool EnsureDefaultTools(AppConfig config)
    {
        var updated = false;
        var defaults = AppConfig.CreateDefault().Tools;

        if (config.Tools.Count == 0)
        {
            config.Tools = defaults.Select(CloneTool).ToList();
            return true;
        }

        foreach (var defaultTool in defaults)
        {
            var existing = config.Tools.FirstOrDefault(x => x.Name.Equals(defaultTool.Name, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                config.Tools.Add(CloneTool(defaultTool));
                updated = true;
                continue;
            }

            if (!string.Equals(existing.Description, defaultTool.Description, StringComparison.Ordinal))
            {
                existing.Description = defaultTool.Description;
                updated = true;
            }

            if (!string.Equals(existing.OfficialDownloadUrl, defaultTool.OfficialDownloadUrl, StringComparison.Ordinal))
            {
                existing.OfficialDownloadUrl = defaultTool.OfficialDownloadUrl;
                updated = true;
            }
        }

        return updated;
    }

    private static ToolDefinition CloneTool(ToolDefinition source)
    {
        return new ToolDefinition
        {
            Name = source.Name,
            Description = source.Description,
            OfficialDownloadUrl = source.OfficialDownloadUrl
        };
    }

    private static bool EnsureDefaultModsRisk(AppConfig config)
    {
        var updated = false;
        var defaults = ModRiskSettings.CreateDefault();

        config.ModsRisk ??= new ModRiskSettings();

        if (config.ModsRisk.CheatTokens.Count == 0)
        {
            config.ModsRisk.CheatTokens = defaults.CheatTokens.ToList();
            updated = true;
        }

        if (config.ModsRisk.SafeTokens.Count == 0)
        {
            config.ModsRisk.SafeTokens = defaults.SafeTokens.ToList();
            updated = true;
        }

        return updated;
    }

    private static bool EnsureDefaultModsScan(AppConfig config)
    {
        if (config.ModsScan is not null)
        {
            return false;
        }

        config.ModsScan = ModsScanSettings.CreateDefault();
        return true;
    }
}
