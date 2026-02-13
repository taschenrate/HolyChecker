namespace HolyChecker.Models;

public sealed class ProcessConnectionInfo
{
    public string LocalAddress { get; init; } = string.Empty;
    public int LocalPort { get; init; }
    public string RemoteAddress { get; init; } = string.Empty;
    public int RemotePort { get; init; }
    public string State { get; init; } = string.Empty;
    public string ServerName { get; init; } = string.Empty;
}