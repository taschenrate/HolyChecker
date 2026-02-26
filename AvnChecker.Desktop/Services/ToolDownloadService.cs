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
        var fileName = ResolveFileName(tool, uri);
        var folder = GetToolFolder(tool);
        var targetPath = Path.Combine(folder, fileName);

        using var response = await _httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            if ((int)response.StatusCode == 618)
            {
                throw new InvalidOperationException(
                    "Ссылка на файл истекла (618 jwt:expired). Обновите URL в appsettings.json."
                );
            }

            response.EnsureSuccessStatusCode();
        }

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var destination = File.Create(targetPath);
        await source.CopyToAsync(destination, cancellationToken);

        progress?.Report("Скачано");
        return targetPath;
    }

    public string GetToolFolder(ToolDefinition tool)
    {
        var folder = Path.Combine(_baseDirectory, "Tools", SanitizeName(tool.Name));
        Directory.CreateDirectory(folder);
        return folder;
    }

    public string GetExpectedDownloadPath(ToolDefinition tool)
    {
        var uri = new Uri(tool.OfficialDownloadUrl);
        var fileName = ResolveFileName(tool, uri);
        return Path.Combine(GetToolFolder(tool), fileName);
    }

    public string? TryGetExistingDownloadPath(ToolDefinition tool)
    {
        var expectedPath = GetExpectedDownloadPath(tool);
        if (File.Exists(expectedPath))
        {
            return expectedPath;
        }

        var folder = GetToolFolder(tool);
        if (!Directory.Exists(folder))
        {
            return null;
        }

        return Directory.EnumerateFiles(folder)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    private static string ResolveFileName(ToolDefinition tool, Uri uri)
    {
        var queryFileName = TryExtractFileNameFromQuery(uri.Query);
        if (!string.IsNullOrWhiteSpace(queryFileName))
        {
            return queryFileName;
        }

        var fileName = Path.GetFileName(uri.LocalPath);
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            return fileName;
        }

        return $"{SanitizeName(tool.Name)}.bin";
    }

    private static string? TryExtractFileNameFromQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        var parts = query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var part in parts)
        {
            var delimiterIndex = part.IndexOf('=');
            if (delimiterIndex <= 0)
            {
                continue;
            }

            var key = Uri.UnescapeDataString(part[..delimiterIndex]);
            if (!key.Equals("response-content-disposition", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var disposition = Uri.UnescapeDataString(part[(delimiterIndex + 1)..]);
            const string marker = "filename=";
            var filenameIndex = disposition.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (filenameIndex < 0)
            {
                continue;
            }

            var fileName = disposition[(filenameIndex + marker.Length)..].Trim(' ', '"', '\'');
            return string.IsNullOrWhiteSpace(fileName) ? null : fileName;
        }

        return null;
    }

    private static string SanitizeName(string input)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var result = new string(input.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
        return result.Trim();
    }
}

