using System.Diagnostics;
using System.Management;
using AvnChecker.Desktop.Models;

namespace AvnChecker.Desktop.Services;

public sealed class ProcessScannerService
{
    private static readonly Dictionary<string, string> SuspiciousProcessRules = new(StringComparer.OrdinalIgnoreCase)
    {
        ["cheatengine"] = "Обнаружен Cheat Engine",
        ["processhacker"] = "Обнаружен Process Hacker",
        ["dnspy"] = "Обнаружен dnSpy",
        ["x64dbg"] = "Обнаружен x64dbg",
        ["ollydbg"] = "Обнаружен OllyDbg",
        ["ida64"] = "Обнаружен IDA",
        ["ida"] = "Обнаружен IDA",
        ["wireshark"] = "Обнаружен Wireshark",
        ["fiddler"] = "Обнаружен Fiddler",
        ["httpdebugger"] = "Обнаружен HTTP Debugger",
        ["charles"] = "Обнаружен Charles Proxy",
        ["injector"] = "Потенциальный инжектор",
        ["ghub"] = "Проверить вспомогательный макрос/хук",
        ["macro"] = "Проверить макро-инструмент"
    };

    public List<ProcessInfoEntry> ScanSuspiciousProcesses()
    {
        var result = new List<ProcessInfoEntry>();
        var commandLineMap = ReadProcessCommandLines();

        foreach (var process in Process.GetProcesses())
        {
            try
            {
                var normalizedName = process.ProcessName.ToLowerInvariant();
                foreach (var rule in SuspiciousProcessRules)
                {
                    if (!normalizedName.Contains(rule.Key, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    result.Add(new ProcessInfoEntry
                    {
                        Name = process.ProcessName,
                        Pid = process.Id,
                        Reason = rule.Value
                    });

                    goto NextProcess;
                }

                if (commandLineMap.TryGetValue(process.Id, out var commandLine))
                {
                    if (commandLine.Contains("--inject", StringComparison.OrdinalIgnoreCase) ||
                        commandLine.Contains("dll", StringComparison.OrdinalIgnoreCase) ||
                        commandLine.Contains("hook", StringComparison.OrdinalIgnoreCase))
                    {
                        result.Add(new ProcessInfoEntry
                        {
                            Name = process.ProcessName,
                            Pid = process.Id,
                            Reason = "Подозрительные аргументы запуска"
                        });
                    }
                }
            }
            catch
            {
                // ignored
            }

        NextProcess:
            process.Dispose();
        }

        return result
            .GroupBy(x => new { x.Pid, x.Name })
            .Select(g => g.First())
            .OrderByDescending(x => x.Name)
            .ToList();
    }

    private static Dictionary<int, string> ReadProcessCommandLines()
    {
        var map = new Dictionary<int, string>();

        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT ProcessId, CommandLine FROM Win32_Process");
            foreach (var entry in searcher.Get())
            {
                if (entry["ProcessId"] is null)
                {
                    continue;
                }

                var pid = Convert.ToInt32(entry["ProcessId"]);
                var commandLine = entry["CommandLine"]?.ToString();
                if (!string.IsNullOrWhiteSpace(commandLine))
                {
                    map[pid] = commandLine;
                }
            }
        }
        catch
        {
            // ignored
        }

        return map;
    }
}
