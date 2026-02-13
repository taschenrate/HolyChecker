using System.Windows;

namespace HolyChecker.ViewModels;

public sealed class MainViewModel : BaseViewModel
{
    public ProcessInfoViewModel ProcessInfo { get; }
    public ToolsViewModel Tools { get; }
    public EverythingQueriesViewModel EverythingQueries { get; }
    public SystemInfoViewModel SystemInfo { get; }

    private string _statusText = "Ready";
    public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value); }

    public MainViewModel()
    {
        try
        {
            ProcessInfo = new ProcessInfoViewModel();
            Tools = new ToolsViewModel();
            EverythingQueries = new EverythingQueriesViewModel();
            SystemInfo = new SystemInfoViewModel();

            StatusText = App.IsRunningAsAdmin() ? "Запущено от администратора" : "Без прав администратора (некоторые функции ограничены)";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.ToString(), "Ошибка в MainViewModel", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}