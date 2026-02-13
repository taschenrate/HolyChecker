using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.ServiceProcess;
using HolyChecker.Models;
using Microsoft.Win32;

namespace HolyChecker.Services;

public sealed class SystemInfoService
{
    public (DateTime? InstallDate, string Version, string Build) GetWindowsInfo()
    {
        DateTime? installDate = null;
        string version = string.Empty;
        string build = string.Empty;

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            if (key != null)
            {
                var installTime = key.GetValue("InstallDate");
                if (installTime is int unixTime)
                    installDate = DateTimeOffset.FromUnixTimeSeconds(unixTime).LocalDateTime;

                var displayVersion = key.GetValue("DisplayVersion")?.ToString();
                var productName = key.GetValue("ProductName")?.ToString();
                version = $"{productName} {displayVersion}";

                var currentBuild = key.GetValue("CurrentBuild")?.ToString();
                var ubr = key.GetValue("UBR")?.ToString();
                build = $"{currentBuild}.{ubr}";
            }
        }
        catch { }

        return (installDate, version, build);
    }

    public List<(string Name, string Status, string StartupType)> CheckServices()
    {
        var serviceNames = new[] { "PcaSvc", "DPS", "SysMain", "EventLog", "bam" };
        var result = new List<(string, string, string)>();

        foreach (var name in serviceNames)
        {
            try
            {
                using var sc = new ServiceController(name);
                result.Add((name, sc.Status.ToString(), sc.StartType.ToString()));
            }
            catch
            {
                result.Add((name, "Not Found", "N/A"));
            }
        }

        return result;
    }

    public (bool Found3079, bool LogsCleared, DateTime? LastClearDate) CheckEventLogs()
    {
        bool found3079 = false;
        bool logsCleared = false;
        DateTime? lastClearDate = null;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "wevtutil",
                Arguments = @"qe System /q:""*[System[(EventID=3079)]]"" /c:1 /f:text",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            if (p != null)
            {
                var output = p.StandardOutput.ReadToEnd();
                p.WaitForExit(10000);
                found3079 = !string.IsNullOrWhiteSpace(output) && output.Contains("Event");
            }
        }
        catch { }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "wevtutil",
                Arguments = @"qe Security /q:""*[System[(EventID=1102)]]"" /c:1 /rd:true /f:text",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            if (p != null)
            {
                var output = p.StandardOutput.ReadToEnd();
                p.WaitForExit(10000);
                if (!string.IsNullOrWhiteSpace(output) && output.Contains("Date"))
                {
                    logsCleared = true;
                    foreach (var line in output.Split('\n'))
                    {
                        var trimmed = line.Trim();
                        if (trimmed.StartsWith("Date:"))
                        {
                            var dateStr = trimmed.Substring("Date:".Length).Trim();
                            if (DateTime.TryParse(dateStr, out var dt))
                                lastClearDate = dt;
                            break;
                        }
                    }
                }
            }
        }
        catch { }

        return (found3079, logsCleared, lastClearDate);
    }

    public DateTime? GetRecycleBinLastModified()
    {
        try
        {
            var drives = DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed);
            DateTime? latest = null;
            foreach (var drive in drives)
            {
                var recyclePath = Path.Combine(drive.RootDirectory.FullName, "$Recycle.Bin");
                if (Directory.Exists(recyclePath))
                {
                    var di = new DirectoryInfo(recyclePath);
                    var lastWrite = di.LastWriteTime;
                    if (latest == null || lastWrite > latest)
                        latest = lastWrite;
                }
            }
            return latest;
        }
        catch { return null; }
    }

    public VirtualMachineInfo DetectVirtualMachine()
    {
        var biosInfo = string.Empty;
        var manufacturer = string.Empty;
        var macAddress = string.Empty;
        var hyperV = false;
        var isVm = false;
        var platform = "Physical Machine";

        try
        {
            using var biosKey = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\BIOS");
            if (biosKey != null)
            {
                biosInfo = biosKey.GetValue("BIOSVendor")?.ToString() ?? string.Empty;
                manufacturer = biosKey.GetValue("SystemManufacturer")?.ToString() ?? string.Empty;
            }
        }
        catch { }

        try
        {
            var nics = NetworkInterface.GetAllNetworkInterfaces();
            foreach (var nic in nics)
            {
                if (nic.OperationalStatus == OperationalStatus.Up && nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                {
                    macAddress = nic.GetPhysicalAddress().ToString();
                    if (!string.IsNullOrEmpty(macAddress)) break;
                }
            }
        }
        catch { }

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Virtual Machine\Guest\Parameters");
            hyperV = key != null;
        }
        catch { }

        var vmIndicators = new (string Pattern, string Name)[]
        {
            ("VMware", "VMware"),
            ("VirtualBox", "VirtualBox"),
            ("VBOX", "VirtualBox"),
            ("Hyper-V", "Hyper-V"),
            ("Microsoft Corporation", "Hyper-V"),
            ("QEMU", "QEMU"),
            ("Xen", "Xen"),
            ("Parallels", "Parallels")
        };

        var combined = $"{biosInfo} {manufacturer}".ToLowerInvariant();
        foreach (var (pat, name) in vmIndicators)
        {
            if (combined.Contains(pat.ToLowerInvariant()))
            {
                isVm = true;
                platform = name;
                break;
            }
        }

        if (!isVm && hyperV)
        {
            isVm = true;
            platform = "Hyper-V";
        }

        var vmMacPrefixes = new[] { "000C29", "005056", "000569", "080027", "0003FF", "001C42" };
        if (!isVm && !string.IsNullOrEmpty(macAddress))
        {
            var cleanMac = macAddress.Replace("-", "").Replace(":", "");
            var macPrefix = cleanMac.Substring(0, Math.Min(6, cleanMac.Length));
            foreach (var prefix in vmMacPrefixes)
            {
                if (macPrefix.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    isVm = true;
                    platform = "VM (MAC-based detection)";
                    break;
                }
            }
        }

        return new VirtualMachineInfo
        {
            IsVirtualMachine = isVm,
            DetectedPlatform = platform,
            BiosInfo = biosInfo,
            Manufacturer = manufacturer,
            MacAddress = macAddress,
            HyperVDetected = hyperV
        };
    }
}