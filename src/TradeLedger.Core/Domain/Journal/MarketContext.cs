namespace TradeLedger.Core.Domain;

public sealed class MarketContext
{
    private MarketContext()
    {
    }

    public string? Total2 { get; private set; }

    public string? BtcDominance { get; private set; }

    public string? UsdtDominance { get; private set; }

    public string? MarketTrend { get; private set; }

    public string? Sma { get; private set; }

    public string? MarketSession { get; private set; }

    public string? BtcPair { get; private set; }

    public string? Rsi { get; private set; }

    public string? Volume { get; private set; }

    public string? CandleShape { get; private set; }

    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(Total2)
        && string.IsNullOrWhiteSpace(BtcDominance)
        && string.IsNullOrWhiteSpace(UsdtDominance)
        && string.IsNullOrWhiteSpace(MarketTrend)
        && string.IsNullOrWhiteSpace(Sma)
        && string.IsNullOrWhiteSpace(MarketSession)
        && string.IsNullOrWhiteSpace(BtcPair)
        && string.IsNullOrWhiteSpace(Rsi)
        && string.IsNullOrWhiteSpace(Volume)
        && string.IsNullOrWhiteSpace(CandleShape);

    public static MarketContext Create(
        string? total2 = null,
        string? btcDominance = null,
        string? usdtDominance = null,
        string? marketTrend = null,
        string? sma = null,
        string? marketSession = null,
        string? btcPair = null,
        string? rsi = null,
        string? volume = null,
        string? candleShape = null) => new()
    {
        Total2 = Trim(total2),
        BtcDominance = Trim(btcDominance),
        UsdtDominance = Trim(usdtDominance),
        MarketTrend = Trim(marketTrend),
        Sma = Trim(sma),
        MarketSession = Trim(marketSession),
        BtcPair = Trim(btcPair),
        Rsi = Trim(rsi),
        Volume = Trim(volume),
        CandleShape = Trim(candleShape),
    };

    private static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
