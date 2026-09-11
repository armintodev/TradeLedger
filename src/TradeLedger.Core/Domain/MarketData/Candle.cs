namespace TradeLedger.Core.Domain.MarketData;

public sealed class Candle
{
    private Candle()
    {
    }

    public long Id { get; private set; }

    public CandleSource Source { get; private set; }

    public string Symbol { get; private set; } = string.Empty;

    public CandleInterval Interval { get; private set; }

    public DateTimeOffset OpenTime { get; private set; }

    public long OpenTimeRawMs { get; private set; }

    public decimal Open { get; private set; }

    public decimal High { get; private set; }

    public decimal Low { get; private set; }

    public decimal Close { get; private set; }

    public decimal Volume { get; private set; }

    public decimal? QuoteVolume { get; private set; }

    public int? TradeCount { get; private set; }

    public DateTimeOffset FetchedAt { get; private set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset CloseTime => OpenTime + Interval.Duration();

    public decimal Range => High - Low;

    public bool IsUp => Close > Open;

    public void AttributeTo(string canonicalSymbol)
    {
        Symbol = Guard.NotBlank(canonicalSymbol, nameof(canonicalSymbol)).ToUpperInvariant();
    }

    public static Candle Of(
        CandleSource source,
        string symbol,
        CandleInterval interval,
        DateTimeOffset openTime,
        decimal open,
        decimal high,
        decimal low,
        decimal close,
        decimal volume,
        decimal? quoteVolume = null,
        int? tradeCount = null,
        long? openTimeRawMs = null)
    {
        var candle = new Candle
        {
            Source = source,
            Symbol = Guard.NotBlank(symbol, nameof(symbol)).ToUpperInvariant(),
            Interval = interval,
            OpenTime = openTime,
            OpenTimeRawMs = openTimeRawMs ?? openTime.ToUnixTimeMilliseconds(),
            Open = Guard.Positive(open, nameof(open)),
            High = Guard.Positive(high, nameof(high)),
            Low = Guard.Positive(low, nameof(low)),
            Close = Guard.Positive(close, nameof(close)),
            Volume = Guard.NotNegative(volume, nameof(volume)),
            QuoteVolume = quoteVolume,
            TradeCount = tradeCount,
        };

        Guard.Rule(
            candle.High >= candle.Low,
            "candle_high_below_low",
            $"A {symbol} {interval} bar at {openTime:O} reports a high of {high} below its low of {low}.");

        Guard.Rule(
            candle.Open <= candle.High && candle.Open >= candle.Low,
            "candle_open_outside_range",
            $"A {symbol} {interval} bar at {openTime:O} opens at {open}, outside its own {low} to {high} range.");

        Guard.Rule(
            candle.Close <= candle.High && candle.Close >= candle.Low,
            "candle_close_outside_range",
            $"A {symbol} {interval} bar at {openTime:O} closes at {close}, outside its own {low} to {high} range.");

        return candle;
    }
}

public sealed class FundingRateHistory
{
    private FundingRateHistory()
    {
    }

    public long Id { get; private set; }

    public CandleSource Source { get; private set; }

    public string Symbol { get; private set; } = string.Empty;

    public DateTimeOffset FundingTime { get; private set; }

    public long FundingTimeRawMs { get; private set; }

    public decimal FundingRate { get; private set; }

    public DateTimeOffset FetchedAt { get; private set; } = DateTimeOffset.UtcNow;

    public void AttributeTo(string canonicalSymbol)
    {
        Symbol = Guard.NotBlank(canonicalSymbol, nameof(canonicalSymbol)).ToUpperInvariant();
    }

    public static FundingRateHistory Of(
        CandleSource source,
        string symbol,
        DateTimeOffset fundingTime,
        decimal fundingRate,
        long? fundingTimeRawMs = null) => new()
    {
        Source = source,
        Symbol = Guard.NotBlank(symbol, nameof(symbol)).ToUpperInvariant(),
        FundingTime = fundingTime,
        FundingTimeRawMs = fundingTimeRawMs ?? fundingTime.ToUnixTimeMilliseconds(),
        FundingRate = fundingRate,
    };
}

public sealed class MarketSymbolAlias
{
    private MarketSymbolAlias()
    {
    }

    public int Id { get; private set; }

    public string CanonicalSymbol { get; private set; } = string.Empty;

    public CandleSource Source { get; private set; }

    public string SourceSymbol { get; private set; } = string.Empty;

    public static MarketSymbolAlias Of(string canonicalSymbol, CandleSource source, string sourceSymbol) => new()
    {
        CanonicalSymbol = Guard.NotBlank(canonicalSymbol, nameof(canonicalSymbol)).ToUpperInvariant(),
        Source = source,
        SourceSymbol = Guard.NotBlank(sourceSymbol, nameof(sourceSymbol)).ToUpperInvariant(),
    };
}

public sealed record CandleGap(DateTimeOffset From, DateTimeOffset To, int MissingCount);

public sealed record CandleCoverage(
    CandleSource Source,
    string Symbol,
    CandleInterval Interval,
    long RowCount,
    DateTimeOffset? FirstOpenTime,
    DateTimeOffset? LastOpenTime);
