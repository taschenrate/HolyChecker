namespace AvnChecker.Desktop.Models;

public sealed class TwinkEntry
{
    public string Nick { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
}

public sealed class ModEntry
{
    public string File { get; set; } = string.Empty;
    public double SizeMb { get; set; }
    public string Path { get; set; } = string.Empty;
    public string Client { get; set; } = string.Empty;
    public string RiskLevel { get; set; } = "unknown";
    public string RiskReason { get; set; } = "Неизвестный мод";
}

public sealed class ProcessInfoEntry
{
    public string Name { get; set; } = string.Empty;
    public int Pid { get; set; }
    public string Reason { get; set; } = string.Empty;
}
