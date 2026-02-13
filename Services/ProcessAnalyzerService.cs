using System.Diagnostics;
using System.IO;
using System.Management;
using System.Text.RegularExpressions;
using HolyChecker.Models;

namespace HolyChecker.Services;

public sealed class ProcessAnalyzerService
{
    private string _cachedCommandLine = string.Empty;
    private int _cachedPid;

    private static readonly string[] McKeywords = {
        "minecraft", ".minecraft", "lunarclient", "badlion", "feather",
        "fabric", "forge", "optifine", "tlauncher", "multimc", "prismlaunch",
        "net.minecraft", "GradleStart", "LaunchWrapper", "cpw.mods",
        "--gameDir", "--assetsDir", "--assetIndex", "--version",
        "authlib-injector", "minecraftforge"
    };

    public (bool IsRunning, int Pid, DateTime? StartTime, string Path) GetJavawInfo()
    {
        try
        {
            foreach (var name in new[] { "javaw", "java" })
            {
                var processes = Process.GetProcessesByName(name);
                foreach (var proc in processes)
                {
                    try
                    {
                        var cmd = GetCommandLine(proc.Id);
                        if (string.IsNullOrEmpty(cmd)) continue;

                        bool isMc = false;
                        foreach (var kw in McKeywords)
                        {
                            if (cmd.Contains(kw, StringComparison.OrdinalIgnoreCase))
                            {
                                isMc = true;
                                break;
                            }
                        }
                        if (!isMc) continue;

                        _cachedCommandLine = cmd;
                        _cachedPid = proc.Id;

                        DateTime? startTime = null;
                        string path = string.Empty;
                        try { startTime = proc.StartTime; } catch { }
                        try { path = proc.MainModule?.FileName ?? string.Empty; } catch { }
                        return (true, proc.Id, startTime, path);
                    }
                    catch { }
                }
            }
        }
        catch { }
        _cachedCommandLine = string.Empty;
        _cachedPid = 0;
        return (false, 0, null, string.Empty);
    }

    public string CachedCommandLine => _cachedCommandLine;

    private string GetCommandLine(int pid)
    {
        if (pid == _cachedPid && _cachedPid != 0 && !string.IsNullOrEmpty(_cachedCommandLine))
            return _cachedCommandLine;
        try
        {
            using var searcher = new ManagementObjectSearcher(
                $"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {pid}");
            foreach (ManagementObject obj in searcher.Get())
                return obj["CommandLine"]?.ToString() ?? string.Empty;
        }
        catch { }
        return string.Empty;
    }

    private static readonly string[] LogSearchPaths = {
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".tlauncher", "legacy", "Minecraft", "game"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".feather", "minecraft"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".lunarclient", "offline", "multiver"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "curseforge", "minecraft", "Install"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PrismLauncher", "instances"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "com.modrinth.theseus", "profiles"),
    };

    private string? FindLatestLog()
    {
        var candidates = new List<string>();

        var gameDirMatch = Regex.Match(_cachedCommandLine, @"--gameDir\s+""?([^""]+?)""?(?:\s|$)", RegexOptions.IgnoreCase);
        if (gameDirMatch.Success)
        {
            var gd = Path.Combine(gameDirMatch.Groups[1].Value.Trim(), "logs", "latest.log");
            if (File.Exists(gd))
                candidates.Add(gd);
        }

        var mcDirMatch = Regex.Match(_cachedCommandLine, @"([\w:\\/.\-]+[/\\]\.minecraft)", RegexOptions.IgnoreCase);
        if (mcDirMatch.Success)
        {
            var md = Path.Combine(mcDirMatch.Groups[1].Value, "logs", "latest.log");
            if (File.Exists(md))
                candidates.Add(md);
        }

        foreach (var basePath in LogSearchPaths)
        {
            var direct = Path.Combine(basePath, "logs", "latest.log");
            if (File.Exists(direct))
                candidates.Add(direct);
        }

        try
        {
            var prismInstances = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PrismLauncher", "instances");
            if (Directory.Exists(prismInstances))
            {
                foreach (var dir in Directory.GetDirectories(prismInstances))
                {
                    var log = Path.Combine(dir, ".minecraft", "logs", "latest.log");
                    if (File.Exists(log)) candidates.Add(log);
                }
            }
        }
        catch { }

        if (candidates.Count == 0) return null;

        return candidates.OrderByDescending(f =>
        {
            try { return new FileInfo(f).LastWriteTime; } catch { return DateTime.MinValue; }
        }).First();
    }

    private List<string> ReadAllLogLines(string path)
    {
        var lines = new List<string>();
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(fs);
            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                if (line != null) lines.Add(line);
            }
        }
        catch { }
        return lines;
    }

    public string ParseVersionFromLog()
    {
        var logPath = FindLatestLog();
        if (logPath == null) return ParseVersionFromCmd(_cachedCommandLine);

        try
        {
            using var fs = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(fs);
            int lineCount = 0;
            while (!reader.EndOfStream && lineCount < 200)
            {
                var line = reader.ReadLine();
                if (line == null) continue;
                lineCount++;

                var m = Regex.Match(line, @"Loading Minecraft\s+([\d.]+)", RegexOptions.IgnoreCase);
                if (m.Success) return m.Groups[1].Value;

                m = Regex.Match(line, @"Minecraft Version[:\s]+([\d.]+)", RegexOptions.IgnoreCase);
                if (m.Success) return m.Groups[1].Value;

                m = Regex.Match(line, @"for Minecraft\s+([\d.]+)", RegexOptions.IgnoreCase);
                if (m.Success) return m.Groups[1].Value;

                m = Regex.Match(line, @"game Minecraft\s+([\d.]+)", RegexOptions.IgnoreCase);
                if (m.Success) return m.Groups[1].Value;

                m = Regex.Match(line, @"Environment:\s+MC\s+([\d.]+)", RegexOptions.IgnoreCase);
                if (m.Success) return m.Groups[1].Value;
            }
        }
        catch { }

        return ParseVersionFromCmd(_cachedCommandLine);
    }

    public string ParseServerFromLog()
    {
        var logPath = FindLatestLog();
        if (logPath == null) return "Лог не найден";

        try
        {
            var lines = ReadAllLogLines(logPath);
            if (lines.Count == 0) return "Лог пуст";

            for (int i = lines.Count - 1; i >= 0; i--)
            {
                var line = lines[i];

                var m = Regex.Match(line, @"Connecting to\s+([^,\s]+),\s*(\d+)", RegexOptions.IgnoreCase);
                if (m.Success)
                {
                    var host = m.Groups[1].Value.TrimEnd('.');
                    var port = m.Groups[2].Value;
                    return port == "25565" ? host : $"{host}:{port}";
                }

                m = Regex.Match(line, @"Connecting to.*?\s+([a-zA-Z0-9._-]+)[:\s]+(\d{2,5})", RegexOptions.IgnoreCase);
                if (m.Success)
                {
                    var host = m.Groups[1].Value.TrimEnd('.');
                    var port = m.Groups[2].Value;
                    return port == "25565" ? host : $"{host}:{port}";
                }
            }

            return "Не подключён (нет записи в логе)";
        }
        catch (Exception ex)
        {
            return $"Ошибка: {ex.Message}";
        }
    }

    private string ParseVersionFromCmd(string commandLine)
    {
        if (string.IsNullOrEmpty(commandLine)) return "Не определено";
        try
        {
            var m = Regex.Match(commandLine, @"--version\s+""?([^\s""]+)""?");
            if (m.Success)
            {
                var raw = m.Groups[1].Value;
                var v = Regex.Match(raw, @"(1\.\d{1,2}(?:\.\d{1,2})?)");
                return v.Success ? v.Groups[1].Value : raw;
            }

            m = Regex.Match(commandLine, @"--assetIndex\s+""?(\d+\.\d+(?:\.\d+)?)""?");
            if (m.Success) return m.Groups[1].Value;

            m = Regex.Match(commandLine, @"versions[/\\]([^/\\""]+)[/\\]");
            if (m.Success)
            {
                var raw = m.Groups[1].Value;
                var v = Regex.Match(raw, @"(1\.\d{1,2}(?:\.\d{1,2})?)");
                return v.Success ? v.Groups[1].Value : raw;
            }

            m = Regex.Match(commandLine, @"\b(1\.\d{1,2}(?:\.\d{1,2})?)\b");
            if (m.Success) return m.Groups[1].Value;
        }
        catch { }
        return "Не определено";
    }

    public List<ProcessConnectionInfo> GetTcpConnections(int pid)
    {
        var result = new List<ProcessConnectionInfo>();
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "netstat",
                Arguments = "-ano -p TCP",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8
            };

            using var process = Process.Start(psi);
            if (process == null) return result;

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);

            foreach (var line in output.Split('\n'))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                var parts = trimmed.Split(Array.Empty<char>(), StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 5) continue;
                if (!parts[0].Equals("TCP", StringComparison.OrdinalIgnoreCase)) continue;
                if (!int.TryParse(parts[4], out var linePid) || linePid != pid) continue;

                ParseEndpoint(parts[1], out var localAddr, out var localPort);
                ParseEndpoint(parts[2], out var remoteAddr, out var remotePort);

                result.Add(new ProcessConnectionInfo
                {
                    LocalAddress = localAddr,
                    LocalPort = localPort,
                    RemoteAddress = remoteAddr,
                    RemotePort = remotePort,
                    State = parts[3],
                    ServerName = $"{remoteAddr}:{remotePort}"
                });
            }
        }
        catch { }
        return result;
    }

    private static void ParseEndpoint(string endpoint, out string address, out int port)
    {
        var lastColon = endpoint.LastIndexOf(':');
        if (lastColon > 0)
        {
            address = endpoint[..lastColon];
            int.TryParse(endpoint[(lastColon + 1)..], out port);
        }
        else
        {
            address = endpoint;
            port = 0;
        }
    }
}