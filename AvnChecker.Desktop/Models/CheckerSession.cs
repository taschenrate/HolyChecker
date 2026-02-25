namespace AvnChecker.Desktop.Models;

public sealed class CheckerSession
{
    public bool IsAuthorized { get; set; }
    public string AccessCode { get; set; } = string.Empty;
    public string CheckerId { get; set; } = string.Empty;
    public string CheckerName { get; set; } = string.Empty;
    public DateTimeOffset? ExpiresAt { get; set; }
}
