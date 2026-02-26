using System.Collections.Concurrent;
using AvnChecker.Desktop.Models;

namespace AvnChecker.Desktop.Services;

public sealed class ModsScannerService
{
    private readonly LoggerService _logger;
    private readonly HashSet<string> _cheatTokens;
    private readonly HashSet<string> _safeTokens;
    private readonly List<string> _cheatCompactTokens;
    private readonly List<string> _safeCompactTokens;
    private readonly ModsScanSettings _scanSettings;

    public ModsScannerService(
        LoggerService logger,
        ModRiskSettings? riskSettings = null,
        ModsScanSettings? scanSettings = null)
    {
        _logger = logger;

        var defaultRisk = ModRiskSettings.CreateDefault();
        var riskSource = riskSettings ?? defaultRisk;
        var cheatTokens = riskSource.CheatTokens.Count > 0 ? riskSource.CheatTokens : defaultRisk.CheatTokens;
        var safeTokens = riskSource.SafeTokens.Count > 0 ? riskSource.SafeTokens : defaultRisk.SafeTokens;

        _cheatTokens = new HashSet<string>(
            cheatTokens.Where(static x => !string.IsNullOrWhiteSpace(x)),
            StringComparer.OrdinalIgnoreCase);
        _safeTokens = new HashSet<string>(
            safeTokens.Where(static x => !string.IsNullOrWhiteSpace(x)),
            StringComparer.OrdinalIgnoreCase);

        _cheatCompactTokens = cheatTokens
            .Select(NormalizeCompact)
            .Where(static x => x.Length >= 4)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        _safeCompactTokens = safeTokens
            .Select(NormalizeCompact)
            .Where(static x => x.Length >= 4)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        _scanSettings = scanSettings ?? ModsScanSettings.CreateDefault();
    }

    public Task<List<ModEntry>> ScanAsync(
        IEnumerable<string> roots,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() => Scan(roots, progress, cancellationToken), cancellationToken);
    }

    private List<ModEntry> Scan(IEnumerable<string> roots, IProgress<int>? progress, CancellationToken cancellationToken)
    {
        var bag = new ConcurrentBag<ModEntry>();
        var total = 0;
        var targets = BuildCandidateFolders(roots).ToList();

        Parallel.ForEach(targets, new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = Environment.ProcessorCount
        }, target =>
        {
            foreach (var filePath in EnumerateModFiles(target.folder))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var fileInfo = new FileInfo(filePath);
                    var mod = new ModEntry
                    {
                        File = fileInfo.Name,
                        SizeMb = Math.Round(fileInfo.Length / 1024d / 1024d, 2),
                        Path = fileInfo.FullName,
                        Client = target.client
                    };

                    var (riskLevel, riskReason) = ClassifyModRisk(mod.File, mod.Path);
                    mod.RiskLevel = riskLevel;
                    mod.RiskReason = riskReason;

                    bag.Add(mod);
                    var current = Interlocked.Increment(ref total);
                    progress?.Report(current);
                }
                catch (Exception ex)
                {
                    _logger.Warn($"Mods scan skip {filePath}: {ex.Message}");
                }
            }
        });

        return bag
            .GroupBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(x => x.File)
            .ToList();
    }

    private IEnumerable<(string folder, string client)> BuildCandidateFolders(IEnumerable<string> roots)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var output = new List<(string folder, string client)>();

        void AddFolder(string path, string client)
        {
            if (Directory.Exists(path) && seen.Add(path))
            {
                output.Add((path, client));
            }
        }

        foreach (var root in roots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var rootInfo = new DirectoryInfo(root);
            var client = string.IsNullOrWhiteSpace(rootInfo.Name) ? "minecraft" : rootInfo.Name;

            AddFolder(Path.Combine(root, "mods"), client);
            AddFolder(Path.Combine(root, "modpacks"), client);

            var versionsDir = Path.Combine(root, "versions");
            if (Directory.Exists(versionsDir))
            {
                foreach (var versionDir in Directory.EnumerateDirectories(versionsDir))
                {
                    AddFolder(Path.Combine(versionDir, "mods"), client);
                }
            }
        }

        foreach (var extra in GetExternalCandidateFolders())
        {
            AddFolder(extra.folder, extra.client);
        }

        return output;
    }

    private IEnumerable<(string folder, string client)> GetExternalCandidateFolders()
    {
        var output = new List<(string folder, string client)>();

        static void TryAdd(List<(string folder, string client)> target, string? path, string client)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                target.Add((path, client));
            }
        }

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (_scanSettings.IncludeDownloads)
        {
            TryAdd(output, Path.Combine(userProfile, "Downloads"), "Downloads");
        }

        if (_scanSettings.IncludeDesktop)
        {
            TryAdd(output, Path.Combine(userProfile, "Desktop"), "Desktop");
        }

        if (_scanSettings.IncludeDocuments)
        {
            TryAdd(output, Path.Combine(userProfile, "Documents"), "Documents");
        }

        if (_scanSettings.IncludeOneDrive)
        {
            var oneDriveRoot = Environment.GetEnvironmentVariable("OneDrive");
            if (!string.IsNullOrWhiteSpace(oneDriveRoot))
            {
                if (_scanSettings.IncludeDownloads)
                {
                    TryAdd(output, Path.Combine(oneDriveRoot, "Downloads"), "OneDrive Downloads");
                }

                if (_scanSettings.IncludeDesktop)
                {
                    TryAdd(output, Path.Combine(oneDriveRoot, "Desktop"), "OneDrive Desktop");
                }

                if (_scanSettings.IncludeDocuments)
                {
                    TryAdd(output, Path.Combine(oneDriveRoot, "Documents"), "OneDrive Documents");
                }
            }
        }

        return output;
    }

    private static IEnumerable<string> EnumerateModFiles(string folder)
    {
        var stack = new Stack<string>();
        stack.Push(folder);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            IEnumerable<string> files;
            IEnumerable<string> directories;

            try
            {
                files = Directory.EnumerateFiles(current);
                directories = Directory.EnumerateDirectories(current);
            }
            catch
            {
                continue;
            }

            foreach (var file in files)
            {
                if (file.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) ||
                    file.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    yield return file;
                }
            }

            foreach (var directory in directories)
            {
                stack.Push(directory);
            }
        }
    }

    private (string riskLevel, string reason) ClassifyModRisk(string fileName, string fullPath)
    {
        var fileWithoutExt = Path.GetFileNameWithoutExtension(fileName);
        var nameTokens = SplitTokens(fileWithoutExt);
        var pathTokens = SplitTokens(fullPath);
        var allTokens = new HashSet<string>(nameTokens, StringComparer.OrdinalIgnoreCase);
        allTokens.UnionWith(pathTokens);

        var matchedCheatToken = allTokens.FirstOrDefault(_cheatTokens.Contains);
        if (!string.IsNullOrWhiteSpace(matchedCheatToken))
        {
            return ("cheat", $"Обнаружен запрещенный признак: {matchedCheatToken}");
        }

        var compactName = NormalizeCompact(fileWithoutExt);
        var matchedCheatCompact = _cheatCompactTokens.FirstOrDefault(token =>
            compactName.Contains(token, StringComparison.Ordinal));
        if (!string.IsNullOrWhiteSpace(matchedCheatCompact))
        {
            return ("cheat", $"Обнаружен запрещенный признак: {matchedCheatCompact}");
        }

        if ((allTokens.Contains("kill") && allTokens.Contains("aura")) ||
            (allTokens.Contains("auto") && (allTokens.Contains("click") || allTokens.Contains("clicker"))) ||
            (allTokens.Contains("crystal") && allTokens.Contains("aura")))
        {
            return ("cheat", "Обнаружена сигнатура чит-мода");
        }

        var matchedSafeToken = allTokens.FirstOrDefault(_safeTokens.Contains);
        if (!string.IsNullOrWhiteSpace(matchedSafeToken))
        {
            return ("safe", "Известный мод");
        }

        var matchedSafeCompact = _safeCompactTokens.FirstOrDefault(token =>
            compactName.Contains(token, StringComparison.Ordinal));
        if (!string.IsNullOrWhiteSpace(matchedSafeCompact))
        {
            return ("safe", "Известный мод");
        }

        return ("unknown", "Неизвестный мод");
    }

    private static HashSet<string> SplitTokens(string value)
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var buffer = new List<char>(value.Length);

        foreach (var ch in value)
        {
            if (char.IsLetterOrDigit(ch))
            {
                buffer.Add(char.ToLowerInvariant(ch));
                continue;
            }

            if (buffer.Count > 1)
            {
                tokens.Add(new string(buffer.ToArray()));
            }

            buffer.Clear();
        }

        if (buffer.Count > 1)
        {
            tokens.Add(new string(buffer.ToArray()));
        }

        return tokens;
    }

    private static string NormalizeCompact(string value)
    {
        return new string(value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
    }
}
