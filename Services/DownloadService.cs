using System.IO;
using System.Net.Http;

namespace HolyChecker.Services;

public sealed class DownloadService : IDownloadService, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _toolsFolder;

    public DownloadService()
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("HolyChecker/1.0");
        _toolsFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "HolyChecker", "Tools");
    }

    public async Task<string> DownloadAsync(string url, string fileName, IProgress<double> progress, CancellationToken token)
    {
        Directory.CreateDirectory(_toolsFolder);
        var filePath = Path.Combine(_toolsFolder, fileName);

        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1;
        long bytesRead = 0;

        await using var contentStream = await response.Content.ReadAsStreamAsync(token);
        await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

        var buffer = new byte[8192];
        int read;
        while ((read = await contentStream.ReadAsync(buffer, token)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, read), token);
            bytesRead += read;
            if (totalBytes > 0)
                progress.Report((double)bytesRead / totalBytes * 100.0);
        }

        progress.Report(100.0);
        return filePath;
    }

    public void Dispose() => _httpClient.Dispose();
}