using System.Net.Http;
using AvnChecker.Desktop.Models;

namespace AvnChecker.Desktop.Services;

public sealed class ToolDownloadService
{
    private readonly HttpClient _httpClient;
    private readonly string _baseDirectory;

    public ToolDownloadService(HttpClient httpClient, string baseDirectory)
    {
        _httpClient = httpClient;
        _baseDirectory = baseDirectory;
    }

    public async Task<string> DownloadAsync(
        ToolDefinition tool,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        progress?.Report("Скачивается...");

        var uri = new Uri(tool.OfficialDownloadUrl);
        var fileName = Path.GetFileName(uri.LocalPath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = $"{SanitizeName(tool.Name)}.bin";
        }

        var folder = Path.Combine(_baseDirectory, "Tools", SanitizeName(tool.Name));
        Directory.CreateDirectory(folder);
        var targetPath = Path.Combine(folder, fileName);

        using var response = await _httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var destination = File.Create(targetPath);
        await source.CopyToAsync(destination, cancellationToken);

        progress?.Report("Скачано");
        return targetPath;
    }

    private static string SanitizeName(string input)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var result = new string(input.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
        return result.Trim();
    }
}

