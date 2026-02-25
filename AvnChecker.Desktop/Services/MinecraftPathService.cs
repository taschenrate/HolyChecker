using System.Diagnostics;
using System.Management;

namespace AvnChecker.Desktop.Services;

public sealed class MinecraftPathService
{
    public IReadOnlyList<string> GetClientRoots(IEnumerable<string>? customPaths = null)
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        TryAddPath(roots, Path.Combine(appData, ".minecraft"));
        TryAddPath(roots, Path.Combine(appData, ".tlauncher", "legacy", "Minecraft", "game"));

        foreach (var activePath in GetActiveClientDirectories())
        {
            TryAddPath(roots, activePath);
        }

        if (customPaths is not null)
        {
            foreach (var customPath in customPaths)
            {
                TryAddPath(roots, customPath);
            }
        }

        return roots.ToList();
    }

    public IReadOnlyList<string> GetActiveClientDirectories()
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var process in Process.GetProcesses())
        {
            try
            {
                var processName = process.ProcessName;
                if (!IsMinecraftRelatedProcess(processName))
                {
                    continue;
                }

                var commandLine = GetProcessCommandLine(process.Id);
                if (!string.IsNullOrWhiteSpace(commandLine))
                {
                    var gameDir = ExtractGameDir(commandLine!);
                    if (!string.IsNullOrWhiteSpace(gameDir))
                    {
                        TryAddPath(result, gameDir!);
                    }
                }

                TryAddFromMainModule(result, process);
            }
            catch
            {
                // ignored: some processes are protected
            }
            finally
            {
                process.Dispose();
            }
        }

        return result.ToList();
    }

    private static bool IsMinecraftRelatedProcess(string processName)
    {
        var normalized = processName.ToLowerInvariant();
        return normalized is "javaw" or "java" ||
               normalized.Contains("minecraft") ||
               normalized.Contains("lunar") ||
               normalized.Contains("badlion") ||
               normalized.Contains("tlauncher") ||
               normalized.Contains("fabric") ||
               normalized.Contains("forge");
    }

    private static void TryAddFromMainModule(HashSet<string> result, Process process)
    {
        try
        {
            var filePath = process.MainModule?.FileName;
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            var directory = Path.GetDirectoryName(filePath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                return;
            }

            if (directory.Contains(".minecraft", StringComparison.OrdinalIgnoreCase))
            {
                var rootIndex = directory.IndexOf(".minecraft", StringComparison.OrdinalIgnoreCase);
                var root = directory[..(rootIndex + ".minecraft".Length)];
                TryAddPath(result, root);
            }

            if (directory.Contains("Minecraft", StringComparison.OrdinalIgnoreCase))
            {
                TryAddPath(result, directory);
            }
        }
        catch
        {
            // ignored
        }
    }

    private static void TryAddPath(HashSet<string> paths, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            var normalized = Path.GetFullPath(Environment.ExpandEnvironmentVariables(path));
            if (Directory.Exists(normalized))
            {
                paths.Add(normalized);
            }
        }
        catch
        {
            // ignored
        }
    }

    private static string? GetProcessCommandLine(int processId)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                $"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {processId}");
            foreach (var item in searcher.Get())
            {
                return item["CommandLine"]?.ToString();
            }
        }
        catch
        {
            // ignored
        }

        return null;
    }

    private static string? ExtractGameDir(string commandLine)
    {
        var marker = "--gameDir";
        var index = commandLine.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return null;
        }

        var tail = commandLine[(index + marker.Length)..].TrimStart();
        if (string.IsNullOrWhiteSpace(tail))
        {
            return null;
        }

        if (tail.StartsWith('"'))
        {
            var endQuote = tail.IndexOf('"', 1);
            if (endQuote > 1)
            {
                return tail[1..endQuote];
            }

            return null;
        }

        var split = tail.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return split.Length > 0 ? split[0] : null;
    }
}
