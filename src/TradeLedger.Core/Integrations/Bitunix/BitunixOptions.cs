namespace TradeLedger.Core.Integrations.Bitunix;

public sealed class BitunixOptions
{
    public const string SectionName = "Bitunix";

    public string BaseUrl { get; set; } = "https://fapi.bitunix.com";

    public string WebSocketPrivateUrl { get; set; } = "wss://fapi.bitunix.com/private";

    public string WebSocketPublicUrl { get; set; } = "wss://fapi.bitunix.com/public";

    public string Language { get; set; } = "en-US";

    public TimestampFormat TimestampFormat { get; set; } = TimestampFormat.EpochMilliseconds;

    public int RequestsPerSecond { get; set; } = 8;

    public int MaxPageSize { get; set; } = 100;

    public TimeSpan HttpTimeout { get; set; } = TimeSpan.FromSeconds(30);

    public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(15);

    public int MaxConnectionsPerServer { get; set; } = 10;

    public TimeSpan PooledConnectionLifetime { get; set; } = TimeSpan.FromMinutes(10);

    public TimeSpan PooledConnectionIdleTimeout { get; set; } = TimeSpan.FromMinutes(2);

    public TimeSpan KeepAlivePingDelay { get; set; } = TimeSpan.FromMinutes(1);

    public TimeSpan KeepAlivePingTimeout { get; set; } = TimeSpan.FromSeconds(30);

    public TimeSpan ReconcileInterval { get; set; } = TimeSpan.FromMinutes(5);

    public TimeSpan SnapshotInterval { get; set; } = TimeSpan.FromMinutes(15);
}
