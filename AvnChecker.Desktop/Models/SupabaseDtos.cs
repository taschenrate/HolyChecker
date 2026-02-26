using System.Text.Json.Serialization;

namespace AvnChecker.Desktop.Models;

public sealed class ValidateCodeResponse
{
    [JsonPropertyName("ok")]
    public bool Ok { get; set; }

    [JsonPropertyName("error_code")]
    public string ErrorCode { get; set; } = "not_found";

    [JsonPropertyName("checker_id")]
    public string? CheckerId { get; set; }

    [JsonPropertyName("checker_name")]
    public string? CheckerName { get; set; }

    [JsonPropertyName("expires_at")]
    public DateTimeOffset? ExpiresAt { get; set; }
}

public sealed class RegisterCheckResponse
{
    [JsonPropertyName("check_id")]
    public long CheckId { get; set; }
}

public sealed class HwidBlacklistCheckResult
{
    [JsonPropertyName("is_blacklisted")]
    public bool IsBlacklisted { get; set; }

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    [JsonPropertyName("source")]
    public string? Source { get; set; }
}
