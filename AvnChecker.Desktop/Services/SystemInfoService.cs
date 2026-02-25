using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using AvnChecker.Desktop.Models;
using Microsoft.Win32;

namespace AvnChecker.Desktop.Services;

public sealed class SystemInfoService
{
    private readonly LoggerService _logger;

    public SystemInfoService(LoggerService logger)
    {
        _logger = logger;
    }

    public Task<SystemReport> CollectAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() => Collect(cancellationToken), cancellationToken);
    }

    private SystemReport Collect(CancellationToken cancellationToken)
    {
        var report = new SystemReport
        {
            ComputerName = Environment.MachineName,
            WindowsUser = Environment.UserName,
            ScreensCount = GetScreensCount(),
            Uptime = FormatUptime(Environment.TickCount64),
            OsName = ReadRegistryString(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "ProductName", "Windows"),
            WindowsVersion = ReadRegistryString(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "DisplayVersion", "Неизвестно"),
            WindowsBuild = ReadRegistryString(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "CurrentBuild", "Неизвестно"),
            OsInstallDate = ReadInstallDate(),
            Cpu = QueryFirstValue("Win32_Processor", "Name", "Неизвестно"),
            Gpu = QueryValues("Win32_VideoController", "Name"),
            Motherboard = QueryFirstValue("Win32_BaseBoard", "Product", "Неизвестно")
        };

        report.IsVm = DetectVirtualMachine();
        report.Hwid = BuildHwid();
        report.EventLogs = new EventLogsReport
        {
            System104 = ReadEventStatus("System", 104),
            Application3079 = ReadEventStatus("Application", 3079)
        };

        cancellationToken.ThrowIfCancellationRequested();
        return report;
    }

    private static int GetScreensCount()
    {
        try
        {
            var monitors = GetSystemMetrics(80);
            return monitors > 0 ? monitors : 1;
        }
        catch
        {
            return 1;
        }
    }

    private static string FormatUptime(long uptimeMs)
    {
        var time = TimeSpan.FromMilliseconds(uptimeMs);
        return $"{time.Days} д {time.Hours} ч {time.Minutes} мин";
    }

    private static string ReadRegistryString(string path, string name, string fallback)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(path);
            return key?.GetValue(name)?.ToString() ?? fallback;
        }
        catch
        {
            return fallback;
        }
    }

    private static string ReadInstallDate()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            var value = key?.GetValue("InstallDate");
            if (value is null)
            {
                return "Неизвестно";
            }

            if (!long.TryParse(value.ToString(), out var seconds))
            {
                return "Неизвестно";
            }

            return DateTimeOffset.FromUnixTimeSeconds(seconds).ToString("yyyy-MM-dd HH:mm:ss zzz");
        }
        catch
        {
            return "Неизвестно";
        }
    }

    private string QueryFirstValue(string className, string propertyName, string fallback)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher($"SELECT {propertyName} FROM {className}");
            foreach (var item in searcher.Get())
            {
                var value = item[propertyName]?.ToString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Warn($"WMI query failed for {className}.{propertyName}: {ex.Message}");
        }

        return fallback;
    }

    private List<string> QueryValues(string className, string propertyName)
    {
        var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var searcher = new ManagementObjectSearcher($"SELECT {propertyName} FROM {className}");
            foreach (var item in searcher.Get())
            {
                var value = item[propertyName]?.ToString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    values.Add(value.Trim());
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Warn($"WMI query list failed for {className}.{propertyName}: {ex.Message}");
        }

        return values.Count == 0 ? ["Неизвестно"] : values.ToList();
    }

    private bool DetectVirtualMachine()
    {
        try
        {
            var model = QueryFirstValue("Win32_ComputerSystem", "Model", string.Empty).ToLowerInvariant();
            var manufacturer = QueryFirstValue("Win32_ComputerSystem", "Manufacturer", string.Empty).ToLowerInvariant();
            var bios = QueryFirstValue("Win32_BIOS", "Version", string.Empty).ToLowerInvariant();

            var vmMarkers = new[] { "virtual", "vmware", "virtualbox", "hyper-v", "kvm", "xen", "qemu", "parallels" };
            if (vmMarkers.Any(marker => model.Contains(marker) || manufacturer.Contains(marker) || bios.Contains(marker)))
            {
                return true;
            }

            var processMarkers = new[] { "vmtoolsd", "vboxservice", "vboxtray", "xenservice" };
            return Process.GetProcesses().Any(p => processMarkers.Contains(p.ProcessName, StringComparer.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            _logger.Warn($"VM detection fallback: {ex.Message}");
            return false;
        }
    }

    private string BuildHwid()
    {
        var cpuId = QueryFirstValue("Win32_Processor", "ProcessorId", "CPU-UNKNOWN");
        var boardSerial = QueryFirstValue("Win32_BaseBoard", "SerialNumber", "BOARD-UNKNOWN");
        var disks = QueryValues("Win32_DiskDrive", "SerialNumber");

        var raw = $"{cpuId}|{boardSerial}|{string.Join("|", disks)}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes);
    }

    private AvnChecker.Desktop.Models.EventLogStatus ReadEventStatus(string logName, int eventId)
    {
        try
        {
            var query = new EventLogQuery(logName, PathType.LogName, $"*[System[(EventID={eventId})]]")
            {
                ReverseDirection = true
            };

            using var reader = new EventLogReader(query);
            using var eventRecord = reader.ReadEvent();
            if (eventRecord is null)
            {
                return new AvnChecker.Desktop.Models.EventLogStatus
                {
                    Status = "НЕ НАЙДЕНО",
                    LastEventTime = "-"
                };
            }

            return new AvnChecker.Desktop.Models.EventLogStatus
            {
                Status = "ОБНАРУЖЕНО",
                LastEventTime = eventRecord.TimeCreated?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-"
            };
        }
        catch (Exception ex)
        {
            _logger.Warn($"Event log read error {logName}/{eventId}: {ex.Message}");
            return new AvnChecker.Desktop.Models.EventLogStatus
            {
                Status = "НЕ НАЙДЕНО",
                LastEventTime = "-"
            };
        }
    }

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);
}

