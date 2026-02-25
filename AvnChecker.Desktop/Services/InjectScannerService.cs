using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using AvnChecker.Desktop.Models;

namespace AvnChecker.Desktop.Services;

public sealed class InjectScannerService
{
    private const int MaxReadBytes = 128 * 1024 * 1024;
    private const int ChunkSize = 64 * 1024;

    private static readonly string[] DpsTags =
    [
        "2023/03/09:17:11:30",
        "2023/07/03:22:01:11",
        "2023/10/04:16:07:05",
        "2024/04/05:11:43:39",
        "2025/05/28:23:57:12",
        "2076/05/18:04:53:15",
        "2024/05/02:22:48",
        "2024/09/06:09:35:50",
        "2023/08/13:13:09:58",
        "2025/01/20:21:37:53",
        "2025/01/16:14:21:21",
        "2022/06/07:07:45:55"
    ];

    private static readonly SignatureRule[] Rules =
    [
        new SignatureRule("doomsday_detected", "Найдены сигнатуры DoomsDay client.",
            ["H(q=Zf@wDLF?/d?D&", "WYX-gih>rssK{||[", "cR_A^yvDVSXDYPQp]"], true),

        new SignatureRule("troxil_detected", "Найдены сигнатуры Troxil client.",
            ["7ba{vsdkvhd}o}w", "Hba{vsdkvhduy{q", "Fba{vsdkvhdpxhz", "Aba{vsdkvhdnxhv"], true),

        new SignatureRule("cortex_detected", "Найдены сигнатуры cortex client.",
            ["cortex", "api.cortexclient.com"], true),

        new SignatureRule("squad_client_detected", "Найдена сигнатура squad client free.",
            ["x/mo/c"], true),

        new SignatureRule("francium_suspicious", "Обнаружена сигнатура Self Destruct.",
            ["Self Destruct"], true),

        new SignatureRule("blessed_detected", "Найдена сигнатура blessed.",
            ["blessed"], true),

        new SignatureRule("stubborn_detected", "Найдена сигнатура stubborn.website.",
            ["stubborn.website"], true),

        new SignatureRule("hitbox_injection", "Найдены признаки Hitbox/ESP внедрений.",
            ["E S P", "Hitbox:", "Reach:", "chs/Main", "chs/Profiler", "net.minecraftforge.ASMEventHandler.31.wait(long, int)"], true),

        new SignatureRule("triggerbot_detected", "Найдена сигнатура TriggerBOT.",
            ["TriggerBOT", "BaoBab:", "b/time", "chs/90", "by.kayn", "ZDCoder", "dreampool", "forge.commons.", "sjuuOiqotmus", "magicthein", "radioegor146"], true)
    ];

    private static readonly Regex[] ExtraRegexPatterns =
    [
        new("net/minecraft/client/renderer/RenderState\\$\\$Lambda\\+0x", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new("net\\.minecraftforge\\.ASMEventHandler\\.31\\.wait\\(long, int\\)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new("TriggerBOT", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new("E\\s*S\\s*P", RegexOptions.IgnoreCase | RegexOptions.Compiled)
    ];

    private readonly LoggerService _logger;

    public InjectScannerService(LoggerService logger)
    {
        _logger = logger;
    }

    public Task<InjectReport> AnalyzeAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() => Analyze(cancellationToken), cancellationToken);
    }

    private InjectReport Analyze(CancellationToken cancellationToken)
    {
        using var process = FindMinecraftProcess();
        if (process is null)
        {
            return new InjectReport
            {
                Found = false,
                Decision = "process_not_found",
                Comment = "Процесс Minecraft/javaw.exe не найден."
            };
        }

        var matches = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var dpsMatches = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var handle = OpenProcess(ProcessAccessFlags.QueryInformation | ProcessAccessFlags.VirtualMemoryRead, false, process.Id);
        if (handle == IntPtr.Zero)
        {
            return new InjectReport
            {
                Found = false,
                Decision = "scan_failed",
                Comment = "Недостаточно прав для чтения памяти процесса."
            };
        }

        try
        {
            GetSystemInfo(out var systemInfo);
            var address = systemInfo.minimumApplicationAddress.ToInt64();
            var maxAddress = systemInfo.maximumApplicationAddress.ToInt64();
            var scanned = 0;
            var buffer = new byte[ChunkSize];

            while (address < maxAddress && scanned < MaxReadBytes)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var queryResult = VirtualQueryEx(handle, new IntPtr(address), out var mbi, (uint)Marshal.SizeOf<MEMORY_BASIC_INFORMATION>());
                if (queryResult == 0)
                {
                    break;
                }

                var regionSize = mbi.RegionSize.ToInt64();
                if (regionSize <= 0)
                {
                    address += 0x1000;
                    continue;
                }

                if (mbi.State == MemoryState.Commit && IsReadable(mbi.Protect))
                {
                    var offset = 0L;
                    while (offset < regionSize && scanned < MaxReadBytes)
                    {
                        var bytesToRead = (int)Math.Min(buffer.Length, regionSize - offset);
                        if (bytesToRead <= 0)
                        {
                            break;
                        }

                        var readOk = ReadProcessMemory(handle, new IntPtr(mbi.BaseAddress.ToInt64() + offset), buffer, bytesToRead, out var bytesRead);
                        if (readOk && bytesRead > 0)
                        {
                            scanned += (int)bytesRead;
                            ScanChunk(buffer, (int)bytesRead, matches, dpsMatches);
                        }

                        offset += bytesToRead;
                    }
                }

                address = mbi.BaseAddress.ToInt64() + regionSize;
            }

            var decision = BuildDecision(matches, out var comment);
            var found = matches.Count > 0;
            return new InjectReport
            {
                Found = found,
                Matches = matches.OrderBy(x => x).ToList(),
                Decision = decision,
                Comment = comment,
                DpsTags = dpsMatches.OrderBy(x => x).ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.Error("Inject scan failed", ex);
            return new InjectReport
            {
                Found = false,
                Decision = "scan_failed",
                Comment = $"Ошибка анализа памяти: {ex.Message}"
            };
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    private static Process? FindMinecraftProcess()
    {
        Process? javaw = null;

        foreach (var process in Process.GetProcesses())
        {
            var name = process.ProcessName.ToLowerInvariant();
            if (name == "javaw" || name == "java")
            {
                if (javaw is null || process.WorkingSet64 > javaw.WorkingSet64)
                {
                    javaw?.Dispose();
                    javaw = process;
                    continue;
                }
            }

            if (name.Contains("minecraft") || name.Contains("lunar") || name.Contains("badlion") || name.Contains("tlauncher"))
            {
                return process;
            }

            process.Dispose();
        }

        return javaw;
    }

    private static bool IsReadable(MemoryProtection protection)
    {
        if (protection.HasFlag(MemoryProtection.Guard) || protection.HasFlag(MemoryProtection.NoAccess))
        {
            return false;
        }

        return protection.HasFlag(MemoryProtection.ReadOnly) ||
               protection.HasFlag(MemoryProtection.ReadWrite) ||
               protection.HasFlag(MemoryProtection.WriteCopy) ||
               protection.HasFlag(MemoryProtection.ExecuteRead) ||
               protection.HasFlag(MemoryProtection.ExecuteReadWrite) ||
               protection.HasFlag(MemoryProtection.ExecuteWriteCopy);
    }

    private static void ScanChunk(byte[] buffer, int length, HashSet<string> matches, HashSet<string> dpsTags)
    {
        if (length <= 0)
        {
            return;
        }

        var chunk = Encoding.Latin1.GetString(buffer, 0, length);

        foreach (var rule in Rules)
        {
            foreach (var token in rule.Tokens)
            {
                if (chunk.Contains(token, StringComparison.OrdinalIgnoreCase))
                {
                    matches.Add(token);
                }
            }
        }

        foreach (var regex in ExtraRegexPatterns)
        {
            foreach (Match match in regex.Matches(chunk))
            {
                if (match.Success)
                {
                    matches.Add(match.Value);
                }
            }
        }

        foreach (var tag in DpsTags)
        {
            if (chunk.Contains(tag, StringComparison.OrdinalIgnoreCase))
            {
                dpsTags.Add(tag);
            }
        }
    }

    private static string BuildDecision(HashSet<string> matches, out string comment)
    {
        foreach (var rule in Rules)
        {
            if (rule.Tokens.Any(token => matches.Contains(token)) && rule.Critical)
            {
                comment = rule.Comment;
                return rule.Decision;
            }
        }

        if (matches.Count > 0)
        {
            comment = "Найдены подозрительные сигнатуры в памяти процесса.";
            return "probably_cheat";
        }

        comment = "Сигнатуры инжектов не обнаружены.";
        return "not_detected";
    }

    private readonly record struct SignatureRule(string Decision, string Comment, IReadOnlyList<string> Tokens, bool Critical);

    [Flags]
    private enum ProcessAccessFlags : uint
    {
        QueryInformation = 0x0400,
        VirtualMemoryRead = 0x0010
    }

    [Flags]
    private enum MemoryState : uint
    {
        Commit = 0x1000
    }

    [Flags]
    private enum MemoryProtection : uint
    {
        NoAccess = 0x01,
        ReadOnly = 0x02,
        ReadWrite = 0x04,
        WriteCopy = 0x08,
        Execute = 0x10,
        ExecuteRead = 0x20,
        ExecuteReadWrite = 0x40,
        ExecuteWriteCopy = 0x80,
        Guard = 0x100
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORY_BASIC_INFORMATION
    {
        public IntPtr BaseAddress;
        public IntPtr AllocationBase;
        public MemoryProtection AllocationProtect;
        public IntPtr RegionSize;
        public MemoryState State;
        public MemoryProtection Protect;
        public uint Type;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEM_INFO
    {
        public ushort processorArchitecture;
        public ushort reserved;
        public uint pageSize;
        public IntPtr minimumApplicationAddress;
        public IntPtr maximumApplicationAddress;
        public IntPtr activeProcessorMask;
        public uint numberOfProcessors;
        public uint processorType;
        public uint allocationGranularity;
        public ushort processorLevel;
        public ushort processorRevision;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(ProcessAccessFlags dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint VirtualQueryEx(IntPtr hProcess, IntPtr lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, uint dwLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, [Out] byte[] lpBuffer, int dwSize, out IntPtr lpNumberOfBytesRead);

    [DllImport("kernel32.dll")]
    private static extern void GetSystemInfo(out SYSTEM_INFO lpSystemInfo);
}
