namespace TradeLedger.Core.Domain;

public sealed class MarketContext
{
    public string? Total2 { get; set; }

    public string? BtcDominance { get; set; }

    public string? UsdtDominance { get; set; }

    public string? MarketTrend { get; set; }
    public string? Sma { get; set; }

    public string? MarketSession { get; set; }

    public string? BtcPair { get; set; }

    public string? Rsi { get; set; }
    public string? Volume { get; set; }
    public string? CandleShape { get; set; }
}
