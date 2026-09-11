namespace TradeLedger.Core.Domain.MarketData;

public enum CandleSource
{
    BinanceFutures = 1,
    BinanceSpot = 2,
    CsvImport = 3,
}

public enum CandleInterval
{
    OneMinute = 1,
    FifteenMinutes = 4,
    ThirtyMinutes = 5,
    OneHour = 6,
    TwoHours = 7,
    FourHours = 8,
    SixHours = 9,
    TwelveHours = 10,
    OneDay = 11,
    OneWeek = 12,
}

public static class CandleIntervals
{
    public const CandleInterval DrillDown = CandleInterval.OneMinute;

    public static readonly IReadOnlyList<CandleInterval> Tradeable =
    [
        CandleInterval.FifteenMinutes,
        CandleInterval.ThirtyMinutes,
        CandleInterval.OneHour,
        CandleInterval.TwoHours,
        CandleInterval.FourHours,
        CandleInterval.SixHours,
        CandleInterval.TwelveHours,
        CandleInterval.OneDay,
        CandleInterval.OneWeek,
    ];

    public static bool IsTradeable(this CandleInterval interval) =>
        interval != CandleInterval.OneMinute;

    public static TimeSpan Duration(this CandleInterval interval) => interval switch
    {
        CandleInterval.OneMinute => TimeSpan.FromMinutes(1),
        CandleInterval.FifteenMinutes => TimeSpan.FromMinutes(15),
        CandleInterval.ThirtyMinutes => TimeSpan.FromMinutes(30),
        CandleInterval.OneHour => TimeSpan.FromHours(1),
        CandleInterval.TwoHours => TimeSpan.FromHours(2),
        CandleInterval.FourHours => TimeSpan.FromHours(4),
        CandleInterval.SixHours => TimeSpan.FromHours(6),
        CandleInterval.TwelveHours => TimeSpan.FromHours(12),
        CandleInterval.OneDay => TimeSpan.FromDays(1),
        CandleInterval.OneWeek => TimeSpan.FromDays(7),
        _ => throw new ArgumentOutOfRangeException(
            nameof(interval), interval, "Unsupported candle interval."),
    };

    public static string ToBinanceInterval(this CandleInterval interval) => interval switch
    {
        CandleInterval.OneMinute => "1m",
        CandleInterval.FifteenMinutes => "15m",
        CandleInterval.ThirtyMinutes => "30m",
        CandleInterval.OneHour => "1h",
        CandleInterval.TwoHours => "2h",
        CandleInterval.FourHours => "4h",
        CandleInterval.SixHours => "6h",
        CandleInterval.TwelveHours => "12h",
        CandleInterval.OneDay => "1d",
        CandleInterval.OneWeek => "1w",
        _ => throw new ArgumentOutOfRangeException(
            nameof(interval), interval, "Unsupported candle interval."),
    };

    public static DateTimeOffset AlignFloor(this CandleInterval interval, DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();

        if (interval == CandleInterval.OneWeek)
        {
            var midnight = new DateTimeOffset(utc.Year, utc.Month, utc.Day, 0, 0, 0, TimeSpan.Zero);
            var daysSinceMonday = ((int)midnight.DayOfWeek + 6) % 7;
            return midnight.AddDays(-daysSinceMonday);
        }

        var ticks = interval.Duration().Ticks;
        var sinceEpoch = utc.UtcTicks - DateTimeOffset.UnixEpoch.UtcTicks;

        return new DateTimeOffset(
            DateTimeOffset.UnixEpoch.UtcTicks + sinceEpoch / ticks * ticks, TimeSpan.Zero);
    }

    public static bool IsAligned(this CandleInterval interval, DateTimeOffset value) =>
        interval.AlignFloor(value) == value.ToUniversalTime();
}
