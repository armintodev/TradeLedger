using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradeLedger.Core.Integrations.Bitunix.Dtos;
using TradeLedger.Core.Shared.Proxy;

namespace TradeLedger.Core.Integrations.Bitunix;

public sealed class BitunixClient(
    IBitunixHttpClientProvider clients,
    IOptions<BitunixOptions> options,
    IBitunixRateLimiter rateLimiter,
    ILogger<BitunixClient> logger
)
{
    private readonly BitunixOptions _options = options.Value;

    public Task<HistoryPositionsPage?> GetHistoryPositionsAsync(
        BitunixConnection connection,
        string? symbol = null,
        long? startTimeMs = null,
        long? endTimeMs = null,
        int skip = 0,
        int limit = 100,
        CancellationToken ct = default)
    {
        var query = new Dictionary<string, string?>
        {
            ["skip"] = skip.ToString(CultureInfo.InvariantCulture),
            ["limit"] = Math.Min(limit, _options.MaxPageSize).ToString(CultureInfo.InvariantCulture),
        };

        AddIfPresent(query, "symbol", symbol);
        AddIfPresent(query, "startTime", startTimeMs?.ToString(CultureInfo.InvariantCulture));
        AddIfPresent(query, "endTime", endTimeMs?.ToString(CultureInfo.InvariantCulture));

        return GetAsync<HistoryPositionsPage>(
            "/api/v1/futures/position/get_history_positions",
            query,
            connection,
            ct
        );
    }

    public Task<HistoryTradesPage?> GetHistoryTradesAsync(
        BitunixConnection connection,
        string? symbol = null,
        long? startTimeMs = null,
        long? endTimeMs = null,
        int skip = 0,
        int limit = 100,
        CancellationToken ct = default)
    {
        var query = new Dictionary<string, string?>
        {
            ["skip"] = skip.ToString(CultureInfo.InvariantCulture),
            ["limit"] = Math.Min(limit, _options.MaxPageSize).ToString(CultureInfo.InvariantCulture),
        };

        AddIfPresent(query, "symbol", symbol);
        AddIfPresent(query, "startTime", startTimeMs?.ToString(CultureInfo.InvariantCulture));
        AddIfPresent(query, "endTime", endTimeMs?.ToString(CultureInfo.InvariantCulture));

        return GetAsync<HistoryTradesPage>(
            "/api/v1/futures/trade/get_history_trades",
            query,
            connection,
            ct
        );
    }

    public Task<List<HistoryPositionDto>?> GetPendingPositionsAsync(
        BitunixConnection connection,
        string? symbol = null,
        CancellationToken ct = default)
    {
        var query = new Dictionary<string, string?>();
        AddIfPresent(query, "symbol", symbol);

        return GetAsync<List<HistoryPositionDto>>(
            "/api/v1/futures/position/get_pending_positions",
            query,
            connection,
            ct
        );
    }

    public Task<FuturesAccountDto?> GetFuturesAccountAsync(
        BitunixConnection connection,
        string marginCoin = "USDT",
        CancellationToken ct = default)
    {
        var query = new Dictionary<string, string?> { ["marginCoin"] = marginCoin };

        return GetAsync<FuturesAccountDto>("/api/v1/futures/account", query, connection, ct);
    }

    public async Task<(bool Ok, string? Error)> VerifyCredentialsAsync(
        BitunixConnection connection,
        CancellationToken ct = default)
    {
        try
        {
            await GetFuturesAccountAsync(connection, ct: ct).ConfigureAwait(false);

            return (true, null);
        }
        catch (ProxyRequiredException ex)
        {
            return (false, ex.Message);
        }
        catch (BitunixApiException ex)
        {
            return (false, ex.Message);
        }
        catch (HttpRequestException ex)
        {
            return (false,
                $"Could not reach Bitunix via {connection.DescribeEgress()}: {ex.Message}");
        }
    }

    private async Task<T?> GetAsync<T>(
        string path,
        Dictionary<string, string?> query,
        BitunixConnection connection,
        CancellationToken ct)
    {
        var credentials = connection.Credentials;
        var http = clients.GetClient(connection.Proxy);

        await rateLimiter.WaitAsync(credentials.ApiKeyHint, ct).ConfigureAwait(false);

        var canonicalQuery = BitunixSigner.CanonicalQuery(query);
        var nonce = BitunixSigner.NewNonce();
        var timestamp = BitunixSigner.Timestamp(DateTimeOffset.UtcNow, _options.TimestampFormat);

        var sign = BitunixSigner.Sign(
            nonce,
            timestamp,
            credentials.ApiKey,
            canonicalQuery,
            string.Empty,
            credentials.ApiSecret
        );

        var url = BuildUrl(path, query);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("api-key", credentials.ApiKey);
        request.Headers.TryAddWithoutValidation("sign", sign);
        request.Headers.TryAddWithoutValidation("nonce", nonce);
        request.Headers.TryAddWithoutValidation("timestamp", timestamp);
        request.Headers.TryAddWithoutValidation("language", _options.Language);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "Bitunix {Path} returned HTTP {Status} via {Egress}",
                path,
                (int)response.StatusCode,
                connection.DescribeEgress()
            );

            throw new BitunixApiException(
                $"Bitunix {path} returned HTTP {(int)response.StatusCode}.",
                (int)response.StatusCode
            );
        }

        BitunixResponse<T>? envelope;

        try
        {
            envelope = JsonSerializer.Deserialize<BitunixResponse<T>>(body, BitunixJson.Options);
        }
        catch (JsonException ex)
        {
            throw new BitunixApiException($"Bitunix {path} returned unreadable JSON.", null, ex);
        }

        if (envelope is null)
        {
            throw new BitunixApiException($"Bitunix {path} returned an empty response.");
        }

        if (!envelope.IsSuccess)
        {
            throw new BitunixApiException(
                $"Bitunix {path} failed: {envelope.Message ?? "unknown error"} (code {envelope.Code}).",
                envelope.Code
            );
        }

        return envelope.Data;
    }

    private string BuildUrl(string path, Dictionary<string, string?> query)
    {
        if (query.Count == 0)
        {
            return path;
        }

        var sb = new StringBuilder(path).Append('?');
        var first = true;

        foreach (var kv in query.Where(k => k.Value is not null))
        {
            if (!first)
            {
                sb.Append('&');
            }

            sb.Append(Uri.EscapeDataString(kv.Key)).Append('=').Append(Uri.EscapeDataString(kv.Value!));
            first = false;
        }

        return sb.ToString();
    }

    private static void AddIfPresent(Dictionary<string, string?> query, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            query[key] = value;
        }
    }
}

public readonly record struct BitunixCredentials(string ApiKey, string ApiSecret, string ApiKeyHint);

public readonly record struct BitunixConnection(BitunixCredentials Credentials, ProxyEndpoint? Proxy)
{
    public string DescribeEgress() => Proxy?.Describe() ?? "direct";
}

public sealed class BitunixApiException(string message, int? code = null, Exception? inner = null)
    : Exception(message, inner)
{
    public int? Code { get; } = code;
}
