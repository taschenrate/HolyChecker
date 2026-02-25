using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AvnChecker.Desktop.Models;

namespace AvnChecker.Desktop.Services;

public sealed class SupabaseService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly HttpClient _httpClient;
    private readonly SupabaseSettings _settings;

    public SupabaseService(HttpClient httpClient, SupabaseSettings settings)
    {
        _httpClient = httpClient;
        _settings = settings;
        _httpClient.Timeout = TimeSpan.FromSeconds(Math.Clamp(settings.RpcTimeoutSeconds, 5, 120));
    }

    public async Task<ValidateCodeResponse> ValidateCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, string>
        {
            ["p_code"] = code
        };

        var response = await PostRpcAsync<ValidateCodeResponse>("validate_code_for_checker", payload, cancellationToken);
        return response ?? new ValidateCodeResponse { Ok = false, ErrorCode = "not_found" };
    }

    public async Task<long> RegisterCheckAsync(
        string code,
        string playerNick,
        string playerHwid,
        string status,
        DateTimeOffset? checkedAt,
        string sourceCodeJson,
        CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, object?>
        {
            ["p_code"] = code,
            ["p_player_nick"] = playerNick,
            ["p_player_hwid"] = playerHwid,
            ["p_status"] = status,
            ["p_checked_at"] = checkedAt?.ToString("O"),
            ["p_source_code"] = sourceCodeJson
        };

        try
        {
            var response = await PostRpcAsync<RegisterCheckResponse>("register_check", payload, cancellationToken);
            return response?.CheckId ?? 0;
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("PGRST202", StringComparison.OrdinalIgnoreCase))
        {
            // Fallback for legacy register_check signature without p_source_code.
            var legacyPayload = new Dictionary<string, object?>
            {
                ["p_code"] = code,
                ["p_player_nick"] = playerNick,
                ["p_player_hwid"] = playerHwid,
                ["p_status"] = status,
                ["p_checked_at"] = checkedAt?.ToString("O")
            };

            var response = await PostRpcAsync<RegisterCheckResponse>("register_check", legacyPayload, cancellationToken);
            return response?.CheckId ?? 0;
        }
    }

    private async Task<T?> PostRpcAsync<T>(string rpcName, object payload, CancellationToken cancellationToken)
        where T : class
    {
        EnsureConfigured();

        var endpoint = $"{_settings.Url.TrimEnd('/')}/rest/v1/rpc/{rpcName}";
        var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json")
        };

        request.Headers.Add("apikey", _settings.ApiKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
        request.Headers.Add("Prefer", "return=representation");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"RPC {rpcName} failed ({(int)response.StatusCode}): {responseBody}");
        }

        var list = JsonSerializer.Deserialize<List<T>>(responseBody, JsonOptions);
        return list?.FirstOrDefault();
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_settings.Url) ||
            string.IsNullOrWhiteSpace(_settings.ApiKey) ||
            _settings.Url.Contains("your-project", StringComparison.OrdinalIgnoreCase) ||
            _settings.ApiKey.Contains("YOUR_SUPABASE_SERVICE_KEY", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Supabase не настроен. Заполните appsettings.json (supabase.url и supabase.api_key)."
            );
        }
    }
}


