using System.Net;
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

    private static readonly string[] BlacklistRpcCandidates =
    [
        "check_hwid_blacklist",
        "is_hwid_blacklisted",
        "get_hwid_blacklist"
    ];

    private static readonly string[] BlacklistTableCandidates =
    [
        "blacklist",
        "hwid_blacklist",
        "black_list",
        "blacklisted_hwids",
        "banned_hwids"
    ];

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
        if (response is null)
        {
            return new ValidateCodeResponse
            {
                Ok = false,
                ErrorCode = "rpc_empty"
            };
        }

        return response;
    }

    public async Task<long> RegisterCheckAsync(
        string code,
        string playerNick,
        string playerHwid,
        string status,
        DateTimeOffset? checkedAt,
        string sourceReportJson,
        CancellationToken cancellationToken = default)
    {
        var basePayload = new Dictionary<string, object?>
        {
            ["p_code"] = code,
            ["p_player_nick"] = playerNick,
            ["p_player_hwid"] = playerHwid,
            ["p_status"] = status,
            ["p_checked_at"] = checkedAt?.ToString("O")
        };

        var parsedReport = TryParseJsonElement(sourceReportJson);

        // Expected for current DB schema:
        // checks.source_code = auth_codes.code
        // checks.source_report = jsonb
        var schemaPayload = new Dictionary<string, object?>(basePayload)
        {
            ["p_source_code"] = code
        };
        if (parsedReport.HasValue)
        {
            schemaPayload["p_source_report"] = parsedReport.Value;
        }

        try
        {
            var response = await PostRpcAsync<RegisterCheckResponse>("register_check", schemaPayload, cancellationToken);
            return response?.CheckId ?? 0;
        }
        catch (InvalidOperationException ex) when (IsMissingRpcArgument(ex))
        {
            // Continue to compatibility fallbacks below.
        }

        // Fallback: RPC may only accept p_source_code.
        var payloadWithSourceCodeOnly = new Dictionary<string, object?>(basePayload)
        {
            ["p_source_code"] = code
        };
        try
        {
            var response = await PostRpcAsync<RegisterCheckResponse>("register_check", payloadWithSourceCodeOnly, cancellationToken);
            var checkId = response?.CheckId ?? 0;
            if (checkId > 0)
            {
                await TryAttachSourceReportAsync(checkId, parsedReport, cancellationToken);
            }

            return checkId;
        }
        catch (InvalidOperationException ex) when (IsMissingRpcArgument(ex))
        {
            // Continue to legacy fallback without p_source_code.
        }

        // Legacy fallback without p_source_code parameter.
        var legacyResponse = await PostRpcAsync<RegisterCheckResponse>("register_check", basePayload, cancellationToken);
        var legacyCheckId = legacyResponse?.CheckId ?? 0;
        if (legacyCheckId > 0)
        {
            await TryAttachSourceReportAsync(legacyCheckId, parsedReport, cancellationToken);
        }

        return legacyCheckId;
    }

    public async Task<HwidBlacklistCheckResult> CheckHwidBlacklistAsync(string hwid, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        var normalizedHwid = (hwid ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalizedHwid))
        {
            return new HwidBlacklistCheckResult();
        }

        foreach (var rpcName in BlacklistRpcCandidates)
        {
            var rpcResult = await TryCheckBlacklistViaRpcAsync(rpcName, normalizedHwid, cancellationToken);
            if (rpcResult.Handled)
            {
                return rpcResult.Result;
            }
        }

        var knownTableResult = await TryCheckBlacklistViaKnownTableAsync(normalizedHwid, cancellationToken);
        if (knownTableResult.Handled)
        {
            return knownTableResult.Result;
        }

        foreach (var tableName in BlacklistTableCandidates)
        {
            var tableResult = await TryCheckBlacklistViaTableAsync(tableName, "hwid", normalizedHwid, cancellationToken);
            if (tableResult.Handled)
            {
                return tableResult.Result;
            }
        }

        return new HwidBlacklistCheckResult
        {
            IsBlacklisted = false,
            Source = "blacklist_check_unavailable",
            Reason = "Нет доступа к blacklist (RLS) или отсутствует RPC check_hwid_blacklist."
        };
    }

    private async Task<T?> PostRpcAsync<T>(string rpcName, object payload, CancellationToken cancellationToken)
        where T : class
    {
        EnsureConfigured();

        var endpoint = $"{_settings.Url.TrimEnd('/')}/rest/v1/rpc/{rpcName}";
        var request = BuildJsonRequest(HttpMethod.Post, endpoint, payload, includePreferHeader: true);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"RPC {rpcName} failed ({(int)response.StatusCode}): {responseBody}");
        }

        var list = JsonSerializer.Deserialize<List<T>>(responseBody, JsonOptions);
        return list?.FirstOrDefault();
    }

    private async Task<(bool Handled, HwidBlacklistCheckResult Result)> TryCheckBlacklistViaKnownTableAsync(
        string hwid,
        CancellationToken cancellationToken)
    {
        try
        {
            var encodedHwid = Uri.EscapeDataString(hwid);
            var endpoint = $"{_settings.Url.TrimEnd('/')}/rest/v1/blacklist?select=hwid,reason,nick&hwid=ilike.{encodedHwid}&limit=1";
            var request = BuildJsonRequest(HttpMethod.Get, endpoint, payload: null, includePreferHeader: false);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                // Table missing or endpoint unavailable: let fallback handle.
                if (response.StatusCode == HttpStatusCode.NotFound ||
                    response.StatusCode == HttpStatusCode.Forbidden ||
                    response.StatusCode == HttpStatusCode.Unauthorized ||
                    (int)response.StatusCode == 400)
                {
                    return (false, new HwidBlacklistCheckResult());
                }
                return (false, new HwidBlacklistCheckResult());
            }

            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength() == 0)
            {
                return (true, new HwidBlacklistCheckResult
                {
                    IsBlacklisted = false,
                    Source = "table:blacklist"
                });
            }

            var row = document.RootElement[0];
            return (true, new HwidBlacklistCheckResult
            {
                IsBlacklisted = true,
                Reason = ExtractReason(row),
                Source = "table:blacklist"
            });
        }
        catch
        {
            return (false, new HwidBlacklistCheckResult());
        }
    }

    private async Task<(bool Handled, HwidBlacklistCheckResult Result)> TryCheckBlacklistViaRpcAsync(
        string rpcName,
        string hwid,
        CancellationToken cancellationToken)
    {
        try
        {
            var endpoint = $"{_settings.Url.TrimEnd('/')}/rest/v1/rpc/{rpcName}";
            var payload = new Dictionary<string, string> { ["p_hwid"] = hwid };
            var request = BuildJsonRequest(HttpMethod.Post, endpoint, payload, includePreferHeader: true);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                if ((int)response.StatusCode is 400 or 404)
                {
                    return (false, new HwidBlacklistCheckResult());
                }

                return (true, new HwidBlacklistCheckResult
                {
                    IsBlacklisted = false,
                    Source = $"rpc:{rpcName}"
                });
            }

            if (string.IsNullOrWhiteSpace(body) || body == "null")
            {
                return (true, new HwidBlacklistCheckResult
                {
                    IsBlacklisted = false,
                    Source = $"rpc:{rpcName}"
                });
            }

            using var document = JsonDocument.Parse(body);
            var firstElement = GetFirstElement(document.RootElement);
            if (!firstElement.HasValue)
            {
                return (true, new HwidBlacklistCheckResult
                {
                    IsBlacklisted = false,
                    Source = $"rpc:{rpcName}"
                });
            }

            var element = firstElement.Value;
            if (TryReadBlacklistFlag(element, out var isBlacklisted))
            {
                return (true, new HwidBlacklistCheckResult
                {
                    IsBlacklisted = isBlacklisted,
                    Reason = ExtractReason(element),
                    Source = $"rpc:{rpcName}"
                });
            }

            if (element.ValueKind == JsonValueKind.Object)
            {
                return (true, new HwidBlacklistCheckResult
                {
                    IsBlacklisted = true,
                    Reason = ExtractReason(element),
                    Source = $"rpc:{rpcName}"
                });
            }

            return (true, new HwidBlacklistCheckResult
            {
                IsBlacklisted = false,
                Source = $"rpc:{rpcName}"
            });
        }
        catch
        {
            return (false, new HwidBlacklistCheckResult());
        }
    }

    private async Task<(bool Handled, HwidBlacklistCheckResult Result)> TryCheckBlacklistViaTableAsync(
        string tableName,
        string columnName,
        string hwid,
        CancellationToken cancellationToken)
    {
        try
        {
            var encodedHwid = Uri.EscapeDataString(hwid);
            var endpoint = $"{_settings.Url.TrimEnd('/')}/rest/v1/{tableName}?select=*&{columnName}=ilike.{encodedHwid}&limit=1";
            var request = BuildJsonRequest(HttpMethod.Get, endpoint, payload: null, includePreferHeader: false);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.NotFound ||
                    response.StatusCode == HttpStatusCode.Forbidden ||
                    response.StatusCode == HttpStatusCode.Unauthorized ||
                    (int)response.StatusCode == 400)
                {
                    return (false, new HwidBlacklistCheckResult());
                }
                return (false, new HwidBlacklistCheckResult());
            }

            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength() == 0)
            {
                return (true, new HwidBlacklistCheckResult
                {
                    IsBlacklisted = false,
                    Source = $"table:{tableName}.{columnName}"
                });
            }

            var row = document.RootElement[0];
            return (true, new HwidBlacklistCheckResult
            {
                IsBlacklisted = true,
                Reason = ExtractReason(row),
                Source = $"table:{tableName}.{columnName}"
            });
        }
        catch
        {
            return (false, new HwidBlacklistCheckResult());
        }
    }

    private async Task TryAttachSourceReportAsync(long checkId, JsonElement? sourceReport, CancellationToken cancellationToken)
    {
        if (checkId <= 0 || !sourceReport.HasValue)
        {
            return;
        }

        try
        {
            var endpoint = $"{_settings.Url.TrimEnd('/')}/rest/v1/checks?id=eq.{checkId}";
            var payload = new Dictionary<string, object?>
            {
                ["source_report"] = sourceReport.Value
            };

            var request = BuildJsonRequest(HttpMethod.Patch, endpoint, payload, includePreferHeader: false);
            request.Headers.Add("Prefer", "return=minimal");

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            _ = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch
        {
            // source_report attach is best effort
        }
    }

    private HttpRequestMessage BuildJsonRequest(HttpMethod method, string endpoint, object? payload, bool includePreferHeader)
    {
        var request = new HttpRequestMessage(method, endpoint);
        if (payload is not null)
        {
            request.Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
        }

        request.Headers.Add("apikey", _settings.ApiKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
        if (includePreferHeader)
        {
            request.Headers.Add("Prefer", "return=representation");
        }

        return request;
    }

    private static JsonElement? GetFirstElement(JsonElement root)
    {
        return root.ValueKind switch
        {
            JsonValueKind.Array when root.GetArrayLength() > 0 => root[0],
            JsonValueKind.Object => root,
            JsonValueKind.True => root,
            JsonValueKind.False => root,
            _ => null
        };
    }

    private static bool TryReadBlacklistFlag(JsonElement element, out bool isBlacklisted)
    {
        if (element.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            isBlacklisted = element.GetBoolean();
            return true;
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var key in new[] { "is_blacklisted", "blacklisted", "is_banned", "banned", "found", "match" })
            {
                if (!element.TryGetProperty(key, out var property))
                {
                    continue;
                }

                if (property.ValueKind is JsonValueKind.True or JsonValueKind.False)
                {
                    isBlacklisted = property.GetBoolean();
                    return true;
                }

                if (property.ValueKind == JsonValueKind.String &&
                    bool.TryParse(property.GetString(), out var parsed))
                {
                    isBlacklisted = parsed;
                    return true;
                }
            }
        }

        isBlacklisted = false;
        return false;
    }

    private static string? ExtractReason(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var key in new[] { "reason", "comment", "note", "details", "description" })
        {
            if (element.TryGetProperty(key, out var property) &&
                property.ValueKind == JsonValueKind.String)
            {
                var value = property.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        return null;
    }

    private static JsonElement? TryParseJsonElement(string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(rawJson);
            return document.RootElement.Clone();
        }
        catch
        {
            return null;
        }
    }

    private static bool IsMissingRpcArgument(InvalidOperationException ex)
    {
        return ex.Message.Contains("PGRST202", StringComparison.OrdinalIgnoreCase) ||
               ex.Message.Contains("Could not find the function", StringComparison.OrdinalIgnoreCase);
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
