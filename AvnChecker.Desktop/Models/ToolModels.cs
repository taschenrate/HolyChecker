using System.ComponentModel;
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

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
