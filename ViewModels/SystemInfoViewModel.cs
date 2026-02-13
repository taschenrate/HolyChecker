using System.Collections.ObjectModel;
using HolyChecker.Models;
using HolyChecker.Services;

namespace HolyChecker.ViewModels;

public sealed class ServiceInfoItem
{
    public string Name { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string StartupType { get; init; } = string.Empty;
}

public sealed class SystemInfoViewModel : BaseViewModel
{
    private readonly SystemInfoService _systemService;

    private string _installDate = string.Empty;
    private string _windowsVersion = string.Empty;
    private string _windowsBuild = string.Empty;
    private bool _isAdmin;

    private bool _event3079Found;
    private bool _logsCleared;
    private string _lastClearDate = string.Empty;
    private string _recycleBinDate = string.Empty;

    private VirtualMachineInfo _vmInfo = new();

    public string InstallDate { get => _installDate; set => SetProperty(ref _installDate, value); }
    public string WindowsVersion { get => _windowsVersion; set => SetProperty(ref _windowsVersion, value); }
    public string WindowsBuild { get => _windowsBuild; set => SetProperty(ref _windowsBuild, value); }
    public bool IsAdmin { get => _isAdmin; set => SetProperty(ref _isAdmin, value); }

    public bool Event3079Found { get => _event3079Found; set => SetProperty(ref _event3079Found, value); }
    public bool LogsCleared { get => _logsCleared; set => SetProperty(ref _logsCleared, value); }
    public string LastClearDate { get => _lastClearDate; set => SetProperty(ref _lastClearDate, value); }
    public string RecycleBinDate { get => _recycleBinDate; set => SetProperty(ref _recycleBinDate, value); }

    public VirtualMachineInfo VmInfo { get => _vmInfo; set => SetProperty(ref _vmInfo, value); }

    public ObservableCollection<ServiceInfoItem> Services { get; } = new();
    public RelayCommand RefreshCommand { get; }

    public SystemInfoViewModel()
    {
        _systemService = new SystemInfoService();
        RefreshCommand = new RelayCommand(_ => LoadSystemInfo());
        LoadSystemInfo();
    }

    private void LoadSystemInfo()
    {
        IsAdmin = App.IsRunningAsAdmin();

        var (installDate, version, build) = _systemService.GetWindowsInfo();
        InstallDate = installDate?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Unknown";
        WindowsVersion = version;
        WindowsBuild = build;

        var (found3079, logsCleared, lastClearDate) = _systemService.CheckEventLogs();
        Event3079Found = found3079;
        LogsCleared = logsCleared;
        LastClearDate = lastClearDate?.ToString("yyyy-MM-dd HH:mm:ss") ?? "N/A";

        var recycleBin = _systemService.GetRecycleBinLastModified();
        RecycleBinDate = recycleBin?.ToString("yyyy-MM-dd HH:mm:ss") ?? "N/A";

        VmInfo = _systemService.DetectVirtualMachine();

        Services.Clear();
        foreach (var (name, status, startupType) in _systemService.CheckServices())
        {
            Services.Add(new ServiceInfoItem { Name = name, Status = status, StartupType = startupType });
        }
    }
}