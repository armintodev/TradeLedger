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

    public int MaxRetries { get; set; } = 4;
}
