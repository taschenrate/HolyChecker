using System.Diagnostics;
using System.IO;
using HolyChecker.Models;

namespace HolyChecker.Services;

public sealed class ExternalToolService : IExternalToolService
{
    private readonly IDownloadService _downloadService;

    public ExternalToolService(IDownloadService downloadService)
    {
        _downloadService = downloadService;
    }

    public async Task LaunchAsync(ExternalTool tool, CancellationToken token)
    {
        tool.IsBusy = true;
        try
        {
            if (string.IsNullOrEmpty(tool.LocalPath) || !File.Exists(tool.LocalPath))
            {
                tool.Status = "Downloading...";
                var progress = new Progress<double>(p => tool.DownloadProgress = p);
                var path = await _downloadService.DownloadAsync(tool.OfficialDownloadUrl, tool.FileName, progress, token);
                tool.LocalPath = path;
                tool.IsInstalled = true;
            }

            if (!File.Exists(tool.LocalPath))
                throw new FileNotFoundException("Downloaded file not found.", tool.LocalPath);

            tool.Status = "Launching...";
            var psi = new ProcessStartInfo
            {
                FileName = tool.LocalPath,
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(tool.LocalPath) ?? string.Empty
            };
            Process.Start(psi);
            tool.Status = "Running";
        }
        catch (OperationCanceledException)
        {
            tool.Status = "Cancelled";
        }
        catch (Exception ex)
        {
            tool.Status = $"Error: {ex.Message}";
        }
        finally
        {
            tool.IsBusy = false;
        }
    }
}