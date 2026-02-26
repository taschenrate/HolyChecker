using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace AvnChecker.Desktop.Models;

public sealed class ToolDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string OfficialDownloadUrl { get; set; } = string.Empty;
}

public sealed class ToolDownloadItem : INotifyPropertyChanged
{
    private string _status = "Не скачано";
    private string? _downloadedPath;
    private bool _isBusy;

    public ToolDownloadItem(ToolDefinition definition)
    {
        Definition = definition;
    }

    public ToolDefinition Definition { get; }

    public string Status
    {
        get => _status;
        set
        {
            if (_status == value)
            {
                return;
            }

            _status = value;
            OnPropertyChanged();
        }
    }

    public string? DownloadedPath
    {
        get => _downloadedPath;
        set
        {
            if (_downloadedPath == value)
            {
                return;
            }

            _downloadedPath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanOpen));
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (_isBusy == value)
            {
                return;
            }

            _isBusy = value;
            OnPropertyChanged();
        }
    }

    public bool CanOpen => !string.IsNullOrWhiteSpace(DownloadedPath) && File.Exists(DownloadedPath);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
