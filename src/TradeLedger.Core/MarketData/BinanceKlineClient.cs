using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradeLedger.Core.Domain.MarketData;
using TradeLedger.Core.Shared.Proxy;

namespace TradeLedger.Core.MarketData;

public interface IKlineSource
{
    Task<IReadOnlyList<Candle>> FetchKlinesAsync(
        CandleSource source,
        string symbol,
        CandleInterval interval,
        DateTimeOffset from,
        DateTimeOffset to,
        ProxyEndpoint? proxy,
        CancellationToken ct = default);

    Task<IReadOnlyList<FundingRateHistory>> FetchFundingRatesAsync(
        string symbol,
        DateTimeOffset from,
        DateTimeOffset to,
        ProxyEndpoint? proxy,
        CancellationToken ct = default);
}

public sealed class BinanceKlineClient(
    IProxiedHttpClientProvider clients,
    IKlineRateLimiter rateLimiter,
    IOptions<MarketDataOptions> options,
    ILogger<BinanceKlineClient> logger) : IKlineSource
{
    public const string FuturesPool = "binance-futures";
    public const string SpotPool = "binance-spot";

    private readonly MarketDataOptions _options = options.Value;

    public async Task<IReadOnlyList<Candle>> FetchKlinesAsync(
        CandleSource source,
        string symbol,
        CandleInterval interval,
        DateTimeOffset from,
        DateTimeOffset to,
        ProxyEndpoint? proxy,
        CancellationToken ct = default)
    {
        if (source == CandleSource.CsvImport)
        {
            throw new ArgumentException(
                "CsvImport candles are supplied by file upload, not fetched.", nameof(source));
        }

        var http = clients.GetClient(SettingsFor(source), proxy);
        var path = source == CandleSource.BinanceFutures ? "/fapi/v1/klines" : "/api/v3/klines";
        var limit = source == CandleSource.BinanceFutures
            ? _options.MaxPageSize
            : _options.MaxSpotPageSize;

        var duration = interval.Duration();
        var cursor = interval.AlignFloor(from);
        var endMs = to.ToUnixTimeMilliseconds();
        var latestClosed = DateTimeOffset.UtcNow - duration;

        var results = new List<Candle>();
        var seen = new HashSet<long>();
        var fetchedAt = DateTimeOffset.UtcNow;

        while (cursor <= to && !ct.IsCancellationRequested)
        {
            var url =
                $"{path}?symbol={Uri.EscapeDataString(symbol)}" +
                $"&interval={interval.ToBinanceInterval()}" +
                $"&startTime={cursor.ToUnixTimeMilliseconds()}" +
                $"&endTime={endMs}" +
                $"&limit={limit}";

            using var document = await SendAsync(http, url, proxy, ct).ConfigureAwait(false);

            var rows = document.RootElement;

            if (rows.ValueKind != JsonValueKind.Array || rows.GetArrayLength() == 0)
            {
                break;
            }

            long lastOpenMs = 0;

            foreach (var row in rows.EnumerateArray())
            {
                var candle = MapKline(row, source, symbol, interval, fetchedAt);
                lastOpenMs = candle.OpenTimeRawMs;

                if (candle.OpenTime < from || candle.OpenTime > to)
                {
                    continue;
                }

                if (candle.OpenTime > latestClosed)
                {
                    continue;
                }

                if (seen.Add(candle.OpenTimeRawMs))
                {
                    results.Add(candle);
                }
            }

            if (lastOpenMs == 0)
            {
                break;
            }

            var next = DateTimeOffset.FromUnixTimeMilliseconds(lastOpenMs) + duration;

            if (next <= cursor)
            {
                break;
            }

            cursor = next;

            if (rows.GetArrayLength() < limit)
            {
                break;
            }
        }

        ct.ThrowIfCancellationRequested();

        logger.LogDebug(
            "Fetched {Count} {Interval} candles for {Symbol} from {Source} via {Egress}.",
            results.Count, interval, symbol, source, proxy?.Describe() ?? "direct");

        return results;
    }

    public async Task<IReadOnlyList<FundingRateHistory>> FetchFundingRatesAsync(
        string symbol,
        DateTimeOffset from,
        DateTimeOffset to,
        ProxyEndpoint? proxy,
        CancellationToken ct = default)
    {
        var http = clients.GetClient(SettingsFor(CandleSource.BinanceFutures), proxy);

        var cursor = from;
        var endMs = to.ToUnixTimeMilliseconds();
        var results = new List<FundingRateHistory>();
        var seen = new HashSet<long>();
        var fetchedAt = DateTimeOffset.UtcNow;

        while (cursor <= to && !ct.IsCancellationRequested)
        {
            var url =
                $"/fapi/v1/fundingRate?symbol={Uri.EscapeDataString(symbol)}" +
                $"&startTime={cursor.ToUnixTimeMilliseconds()}" +
                $"&endTime={endMs}&limit=1000";

            using var document = await SendAsync(http, url, proxy, ct).ConfigureAwait(false);

            var rows = document.RootElement;

            if (rows.ValueKind != JsonValueKind.Array || rows.GetArrayLength() == 0)
            {
                break;
            }

            long lastMs = 0;

            foreach (var row in rows.EnumerateArray())
            {
                var fundingMs = row.GetProperty("fundingTime").GetInt64();
                lastMs = fundingMs;

                if (!seen.Add(fundingMs))
                {
                    continue;
                }

                results.Add(FundingRateHistory.Of(
                    CandleSource.BinanceFutures,
                    symbol,
                    DateTimeOffset.FromUnixTimeMilliseconds(fundingMs),
                    ParseDecimal(row.GetProperty("fundingRate")),
                    fundingMs));
            }

            if (lastMs == 0 || rows.GetArrayLength() < 1000)
            {
                break;
            }

            cursor = DateTimeOffset.FromUnixTimeMilliseconds(lastMs + 1);
        }

        ct.ThrowIfCancellationRequested();

        return results;
    }

    private async Task<JsonDocument> SendAsync(
        HttpClient http,
        string url,
        ProxyEndpoint? proxy,
        CancellationToken ct)
    {
        var egressKey = proxy?.CacheKey ?? "direct";

        for (var attempt = 0; ; attempt++)
        {
            await rateLimiter.WaitAsync(egressKey, ct).ConfigureAwait(false);

            using var response = await http.GetAsync(url, ct).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                return await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
            }

            var status = (int)response.StatusCode;
            var retryable = status is 429 or 418 or >= 500;

            if (!retryable || attempt >= _options.MaxRetries)
            {
                var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

                throw new MarketDataException(
                    $"Binance returned {status} for {url}: {Truncate(body, 500)}", status);
            }

            var delay = response.Headers.RetryAfter?.Delta
                ?? TimeSpan.FromSeconds(Math.Pow(2, attempt));

            logger.LogWarning(
                "Binance returned {Status}; retrying in {Delay} (attempt {Attempt}).",
                status, delay, attempt + 1);

            await Task.Delay(delay, ct).ConfigureAwait(false);
        }
    }

    private static Candle MapKline(
        JsonElement row,
        CandleSource source,
        string symbol,
        CandleInterval interval,
        DateTimeOffset fetchedAt)
    {
        if (row.ValueKind != JsonValueKind.Array || row.GetArrayLength() < 6)
        {
            throw new MarketDataException(
                "Binance kline row was not an array of at least six elements.", null);
        }

        var openMs = row[0].GetInt64();

        return Candle.Of(
            source,
            symbol,
            interval,
            DateTimeOffset.FromUnixTimeMilliseconds(openMs),
            ParseDecimal(row[1]),
            ParseDecimal(row[2]),
            ParseDecimal(row[3]),
            ParseDecimal(row[4]),
            ParseDecimal(row[5]),
            row.GetArrayLength() > 7 ? ParseDecimal(row[7]) : null,
            row.GetArrayLength() > 8 && row[8].ValueKind == JsonValueKind.Number
                ? row[8].GetInt32()
                : null,
            openMs);
    }

    private static decimal ParseDecimal(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Number => element.GetDecimal(),
        JsonValueKind.String => decimal.Parse(
            element.GetString()!,
            NumberStyles.Float,
            CultureInfo.InvariantCulture),
        _ => throw new MarketDataException(
            $"Expected a number or numeric string but got {element.ValueKind}.", null),
    };

    private HttpClientPoolSettings SettingsFor(CandleSource source) => new()
    {
        PoolName = source == CandleSource.BinanceFutures ? FuturesPool : SpotPool,
        BaseAddress = new Uri(source == CandleSource.BinanceFutures
            ? _options.BinanceFuturesBaseUrl
            : _options.BinanceSpotBaseUrl),
        Timeout = _options.HttpTimeout,
        ConnectTimeout = _options.ConnectTimeout,
        MaxConnectionsPerServer = _options.MaxConnectionsPerServer,
        PooledConnectionLifetime = _options.PooledConnectionLifetime,
        PooledConnectionIdleTimeout = _options.PooledConnectionIdleTimeout,
    };

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}

public sealed class MarketDataException(string message, int? statusCode = null)
    : Exception(message)
{
    public int? StatusCode { get; } = statusCode;
}
