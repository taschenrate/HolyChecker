using System.Collections.ObjectModel;
using System.Threading.Tasks;
using HolyChecker.Models;
using HolyChecker.Services;

namespace HolyChecker.ViewModels;

public sealed class ProcessInfoViewModel : BaseViewModel
{
    private readonly ProcessAnalyzerService _analyzer = new();
    private bool _isRunning;
    private int _pid;
    private string _startTime = "N/A";
    private string _processPath = "N/A";
    private string _statusText = "Загрузка...";
    private string _version = "N/A";
    private string _serverIp = "N/A";

    public bool IsRunning { get => _isRunning; set => SetProperty(ref _isRunning, value); }
    public int Pid { get => _pid; set => SetProperty(ref _pid, value); }
    public string StartTime { get => _startTime; set => SetProperty(ref _startTime, value); }
    public string ProcessPath { get => _processPath; set => SetProperty(ref _processPath, value); }
    public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value); }
    public string Version { get => _version; set => SetProperty(ref _version, value); }
    public string ServerIp { get => _serverIp; set => SetProperty(ref _serverIp, value); }
    public ObservableCollection<ProcessConnectionInfo> Connections { get; } = new();
    public RelayCommand RefreshCommand { get; }

    public ProcessInfoViewModel()
    {
        RefreshCommand = new RelayCommand(_ => _ = RefreshAsync());
        _ = RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        StatusText = "Поиск процесса...";

        var result = await Task.Run(() =>
        {
            var info = _analyzer.GetJavawInfo();
            string version = "Не определено", serverIp = "Не подключён";
            List<ProcessConnectionInfo>? conns = null;

            if (info.IsRunning)
            {
                version = _analyzer.ParseVersionFromLog();
                serverIp = _analyzer.ParseServerFromLog();
                conns = _analyzer.GetTcpConnections(info.Pid);
            }

            return (info, version, serverIp, conns);
        });

        IsRunning = result.info.IsRunning;
        Pid = result.info.Pid;
        StartTime = result.info.StartTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "N/A";
        ProcessPath = string.IsNullOrEmpty(result.info.Path) ? "N/A" : result.info.Path;
        StatusText = result.info.IsRunning ? "javaw.exe запущен" : "javaw.exe не найден";
        Version = result.version;
        ServerIp = result.serverIp;

        Connections.Clear();
        if (result.conns != null)
        {
            foreach (var conn in result.conns)
                Connections.Add(conn);
        }
    }
}