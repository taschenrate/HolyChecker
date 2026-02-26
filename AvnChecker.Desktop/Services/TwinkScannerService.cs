using System.Text.Json;
using System.Text.RegularExpressions;
using AvnChecker.Desktop.Models;

namespace AvnChecker.Desktop.Services;

public sealed class TwinkScannerService
{
    private static readonly Regex FallbackNickRegex = new("\"(?:name|username|nick)\"\\s*:\\s*\"(?<value>[^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly LoggerService _logger;

    public TwinkScannerService(LoggerService logger)
    {
        _logger = logger;
    }

    public Task<List<TwinkEntry>> ScanAsync(IEnumerable<string> roots, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => Scan(roots, cancellationToken), cancellationToken);
    }

    private List<TwinkEntry> Scan(IEnumerable<string> roots, CancellationToken cancellationToken)
    {
        var result = new List<TwinkEntry>();

        foreach (var root in roots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var target in BuildTargets(root))
            {
                if (!File.Exists(target.path))
                {
                    continue;
                }

                try
                {
                    var nicks = ReadNicknames(target.path, target.keys);
                    foreach (var nick in nicks)
                    {
                        result.Add(new TwinkEntry
                        {
                            Nick = nick,
                            Source = target.source,
                            Path = target.path
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warn($"Twink parse error: {target.path} => {ex.Message}");
                }
            }
        }

        return result
            .Where(x => !string.IsNullOrWhiteSpace(x.Nick))
            .GroupBy(
                x => $"{x.Nick.ToLowerInvariant()}|{x.Source}|{x.Path}",
                StringComparer.OrdinalIgnoreCase
            )
            .Select(g => g.First())
            .OrderBy(x => x.Nick)
            .ToList();
    }

    private static IEnumerable<(string path, string source, HashSet<string> keys)> BuildTargets(string root)
    {
        var usernameKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "username", "name", "nick" };

        yield return (Path.Combine(root, "tlauncher_profiles.json"), "tlauncher_profiles.json", usernameKeys);
        yield return (Path.Combine(root, "usercache.json"), "usercache.json", usernameKeys);
        yield return (Path.Combine(root, "LabyMod", "accounts.json"), "LabyMod/accounts.json", usernameKeys);
        yield return (Path.Combine(root, "labymod-neo", "accounts.json"), "labymod-neo/accounts.json", usernameKeys);
        yield return (Path.Combine(root, "config", "ias.json"), "config/ias.json", usernameKeys);
    }

    private static List<string> ReadNicknames(string path, HashSet<string> keys)
    {
        var raw = File.ReadAllText(path);
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var json = JsonDocument.Parse(raw);
            ExtractFromElement(json.RootElement, keys, set);
        }
        catch
        {
            foreach (Match match in FallbackNickRegex.Matches(raw))
            {
                var value = match.Groups["value"].Value.Trim();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    set.Add(value);
                }
            }
        }

        return set.ToList();
    }

    private static void ExtractFromElement(JsonElement element, HashSet<string> keys, HashSet<string> output)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (keys.Contains(property.Name) && property.Value.ValueKind == JsonValueKind.String)
                    {
                        var nick = property.Value.GetString();
                        if (!string.IsNullOrWhiteSpace(nick))
                        {
                            output.Add(nick.Trim());
                        }
                    }

                    ExtractFromElement(property.Value, keys, output);
                }
                break;

            case JsonValueKind.Array:
                foreach (var child in element.EnumerateArray())
                {
                    ExtractFromElement(child, keys, output);
                }
                break;
        }
    }
}
