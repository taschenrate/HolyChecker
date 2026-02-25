using System.Collections.Concurrent;
using AvnChecker.Desktop.Models;

namespace AvnChecker.Desktop.Services;

public sealed class ModsScannerService
{
    private readonly LoggerService _logger;

    public ModsScannerService(LoggerService logger)
    {
        _logger = logger;
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

    private static IEnumerable<(string folder, string client)> BuildCandidateFolders(IEnumerable<string> roots)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var output = new List<(string folder, string client)>();

        foreach (var root in roots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var client = new DirectoryInfo(root).Name;
            var local = new List<(string folder, string client)>();

            void Add(string path)
            {
                if (Directory.Exists(path) && seen.Add(path))
                {
                    local.Add((path, client));
                }
            }

            Add(Path.Combine(root, "mods"));
            Add(Path.Combine(root, "modpacks"));

            var versionsDir = Path.Combine(root, "versions");
            if (Directory.Exists(versionsDir))
            {
                foreach (var versionDir in Directory.EnumerateDirectories(versionsDir))
                {
                    Add(Path.Combine(versionDir, "mods"));
                }
            }

            output.AddRange(local);
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
}
