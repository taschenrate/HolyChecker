using System.Text.Json.Serialization;

namespace AvnChecker.Desktop.Models;

public sealed class AppConfig
{
    [JsonPropertyName("supabase")]
    public SupabaseSettings Supabase { get; set; } = new();

    [JsonPropertyName("logging")]
    public LoggingSettings Logging { get; set; } = new();

    [JsonPropertyName("interface_language")]
    public string InterfaceLanguage { get; set; } = "ru";

    [JsonPropertyName("custom_client_paths")]
    public List<string> CustomClientPaths { get; set; } = [];

    [JsonPropertyName("tools")]
    public List<ToolDefinition> Tools { get; set; } = [];

    public static AppConfig CreateDefault()
    {
        return new AppConfig
        {
            Supabase = new SupabaseSettings
            {
                Url = "https://vravdhytgjmvjoksjuyc.supabase.co",
                ApiKey = "sb_publishable_MG2uCAw3bHOA8CXH77s8gw_WxMDislN",
                RpcTimeoutSeconds = 15
            },
            Logging = new LoggingSettings
            {
                Mode = "verbose"
            },
            InterfaceLanguage = "ru",
            Tools =
            [
                new ToolDefinition
                {
                    Name = "Everything",
                    Description = "Быстрый поиск файлов",
                    OfficialDownloadUrl = "https://www.voidtools.com/Everything-1.4.1.1026.x64-Setup.exe"
                },
                new ToolDefinition
                {
                    Name = "ShellBagsView",
                    Description = "Анализ ShellBag артефактов",
                    OfficialDownloadUrl = "https://www.nirsoft.net/utils/shellbags_view.zip"
                },
                new ToolDefinition
                {
                    Name = "ProcMon",
                    Description = "Microsoft Process Monitor",
                    OfficialDownloadUrl = "https://download.sysinternals.com/files/ProcessMonitor.zip"
                },
                new ToolDefinition
                {
                    Name = "Autoruns",
                    Description = "Microsoft Autoruns",
                    OfficialDownloadUrl = "https://download.sysinternals.com/files/Autoruns.zip"
                }
            ]
        };
    }
}

public sealed class SupabaseSettings
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("api_key")]
    public string ApiKey { get; set; } = string.Empty;

    [JsonPropertyName("rpc_timeout_seconds")]
    public int RpcTimeoutSeconds { get; set; } = 15;
}

public sealed class LoggingSettings
{
    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "verbose";
}

