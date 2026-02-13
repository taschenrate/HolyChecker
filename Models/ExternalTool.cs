using HolyChecker.ViewModels;

namespace HolyChecker.Models;

public sealed class ExternalTool : BaseViewModel
{
    private string _name = string.Empty;
    private string _description = string.Empty;
    private string _officialDownloadUrl = string.Empty;
    private string _fileName = string.Empty;
    private string _localPath = string.Empty;
    private bool _isInstalled;
    private bool _isBusy;
    private string _status = string.Empty;
    private double _downloadProgress;

    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public string Description { get => _description; set => SetProperty(ref _description, value); }
    public string OfficialDownloadUrl { get => _officialDownloadUrl; set => SetProperty(ref _officialDownloadUrl, value); }
    public string FileName { get => _fileName; set => SetProperty(ref _fileName, value); }
    public string LocalPath { get => _localPath; set => SetProperty(ref _localPath, value); }
    public bool IsInstalled { get => _isInstalled; set => SetProperty(ref _isInstalled, value); }
    public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }
    public string Status { get => _status; set => SetProperty(ref _status, value); }
    public double DownloadProgress { get => _downloadProgress; set => SetProperty(ref _downloadProgress, value); }
}