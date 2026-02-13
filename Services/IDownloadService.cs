namespace HolyChecker.Services;

public interface IDownloadService
{
    Task<string> DownloadAsync(string url, string fileName, IProgress<double> progress, CancellationToken token);
}