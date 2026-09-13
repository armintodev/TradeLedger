using TradeLedger.Core.Domain.MarketData;

namespace TradeLedger.Core.MarketData;

public sealed class MarketDataOptions
{
    public const string SectionName = "MarketData";

    public string BinanceFuturesBaseUrl { get; set; } = "https://fapi.binance.com";

    public string BinanceSpotBaseUrl { get; set; } = "https://api.binance.com";

    public int RequestsPerSecond { get; set; } = 20;

    public int MaxPageSize { get; set; } = 1500;

    public int MaxSpotPageSize { get; set; } = 1000;

    public TimeSpan HttpTimeout { get; set; } = TimeSpan.FromSeconds(30);

    public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(15);

    public int MaxConnectionsPerServer { get; set; } = 10;

    public TimeSpan PooledConnectionLifetime { get; set; } = TimeSpan.FromMinutes(10);

    public TimeSpan PooledConnectionIdleTimeout { get; set; } = TimeSpan.FromMinutes(2);

    public long MaxImportBytes { get; set; } = 52_428_800;

    /// Kestrel's own request body limit defaults to about 30 MB, well under
    /// MaxImportBytes, so without this a file in between is rejected by the server
    /// before the endpoint runs — as a malformed_request, which is not the error the
    /// user should see. Program.cs raises Kestrel to this figure so the configured
    /// limit is the one that actually decides, and the headroom covers the multipart
    /// envelope: the request body is the file plus its boundaries and headers, so a
    /// file exactly at MaxImportBytes still arrives as a slightly larger body.
    public long MultipartHeadroomBytes { get; set; } = 1_048_576;

    public long MaxRequestBodyBytes => MaxImportBytes + MultipartHeadroomBytes;

    public int MaxRetries { get; set; } = 4;
}
