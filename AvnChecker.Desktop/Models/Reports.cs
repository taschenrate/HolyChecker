using System.Text.Json.Serialization;

namespace AvnChecker.Desktop.Models;

public sealed class SystemReport
{
    [JsonPropertyName("computer_name")]
    public string ComputerName { get; set; } = string.Empty;

    [JsonPropertyName("windows_user")]
    public string WindowsUser { get; set; } = string.Empty;

    [JsonPropertyName("screens_count")]
    public int ScreensCount { get; set; }

    [JsonPropertyName("uptime")]
    public string Uptime { get; set; } = string.Empty;

    [JsonPropertyName("os_name")]
    public string OsName { get; set; } = string.Empty;

    [JsonPropertyName("os_install_date")]
    public string OsInstallDate { get; set; } = "Неизвестно";

    [JsonPropertyName("windows_version")]
    public string WindowsVersion { get; set; } = string.Empty;

    [JsonPropertyName("windows_build")]
    public string WindowsBuild { get; set; } = string.Empty;

    [JsonPropertyName("cpu")]
    public string Cpu { get; set; } = string.Empty;

    [JsonPropertyName("gpu")]
    public List<string> Gpu { get; set; } = [];

    [JsonPropertyName("motherboard")]
    public string Motherboard { get; set; } = "Неизвестно";

    [JsonPropertyName("is_vm")]
    public bool IsVm { get; set; }

    [JsonPropertyName("hwid")]
    public string Hwid { get; set; } = string.Empty;

    [JsonPropertyName("event_logs")]
    public EventLogsReport EventLogs { get; set; } = new();
}

public sealed class EventLogsReport
{
    [JsonPropertyName("system_104")]
    public EventLogStatus System104 { get; set; } = new();

    [JsonPropertyName("application_3079")]
    public EventLogStatus Application3079 { get; set; } = new();
}

public sealed class EventLogStatus
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "НЕ НАЙДЕНО";

    [JsonPropertyName("last_event_time")]
    public string LastEventTime { get; set; } = "-";
}

public sealed class InjectReport
{
    [JsonPropertyName("found")]
    public bool Found { get; set; }

    [JsonPropertyName("matches")]
    public List<string> Matches { get; set; } = [];

    [JsonPropertyName("decision")]
    public string Decision { get; set; } = "not_detected";

    [JsonPropertyName("comment")]
    public string Comment { get; set; } = string.Empty;

    [JsonPropertyName("dps_tags")]
    public List<string> DpsTags { get; set; } = [];
}

public sealed class ExtraReport
{
    [JsonPropertyName("processes")]
    public List<ProcessInfoEntry> Processes { get; set; } = [];

    [JsonPropertyName("file_changes")]
    public List<string> FileChanges { get; set; } = [];

    [JsonPropertyName("dps_tags")]
    public List<string> DpsTags { get; set; } = [];

    [JsonPropertyName("dll_scan")]
    public List<string> DllScan { get; set; } = [];
}

public sealed class CheckSourceReport
{
    [JsonPropertyName("system")]
    public SystemReport System { get; set; } = new();

    [JsonPropertyName("twinks")]
    public List<TwinkEntry> Twinks { get; set; } = [];

    [JsonPropertyName("mods")]
    public List<ModEntry> Mods { get; set; } = [];

    [JsonPropertyName("injects")]
    public InjectReport Injects { get; set; } = new();

    [JsonPropertyName("extra")]
    public ExtraReport Extra { get; set; } = new();

    [JsonPropertyName("notes")]
    public string Notes { get; set; } = string.Empty;
}
