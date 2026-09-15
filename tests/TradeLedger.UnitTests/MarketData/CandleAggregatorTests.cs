using TradeLedger.Core.Domain.MarketData;
using TradeLedger.Core.MarketData;

namespace TradeLedger.UnitTests.MarketData;

public class CandleAggregatorTests
{
    private static readonly DateTimeOffset Monday =
        new(1970, 1, 5, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SixteenFifteenMinuteBarsBecomeOneFourHourBar()
    {
        var bars = FifteenMinutes(16);

        var aggregated = CandleAggregator.Aggregate(bars, CandleInterval.FourHours);

        var bar = Assert.Single(aggregated);

        Assert.Equal(CandleInterval.FourHours, bar.Interval);
        Assert.Equal(DateTimeOffset.UnixEpoch, bar.OpenTime);
        Assert.Equal(bars[0].Open, bar.Open);
        Assert.Equal(bars[^1].Close, bar.Close);
        Assert.Equal(bars.Max(b => b.High), bar.High);
        Assert.Equal(bars.Min(b => b.Low), bar.Low);
        Assert.Equal(bars.Sum(b => b.Volume), bar.Volume);
    }

    [Fact]
    public void ABucketStartingBeforeTheLoadedRangeIsDropped()
    {
        // Opening at 00:15 leaves the 00:00 bucket seven bars short. Keeping it would put a
        // mid-bucket price in Open and seed every Wilder indicator off a bar that never was.
        var bars = FifteenMinutes(32).Skip(1).ToList();

        var aggregated = CandleAggregator.Aggregate(bars, CandleInterval.FourHours);

        var bar = Assert.Single(aggregated);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddHours(4), bar.OpenTime);
    }

    [Fact]
    public void ABucketEndingAfterTheLoadedRangeIsDropped()
    {
        var bars = FifteenMinutes(17);

        var aggregated = CandleAggregator.Aggregate(bars, CandleInterval.FourHours);

        var bar = Assert.Single(aggregated);
        Assert.Equal(DateTimeOffset.UnixEpoch, bar.OpenTime);
    }

    [Fact]
    public void AnInteriorBucketMissingBarsIsBuiltAnywayAndWarnedAbout()
    {
        // Dropping it instead would leave the series contiguous in index but not in time, and
        // every smoothed indicator would silently average across the hole.
        var bars = FifteenMinutes(32);
        bars.RemoveAt(20);

        var warnings = new List<string>();
        var aggregated = CandleAggregator.Aggregate(bars, CandleInterval.FourHours, warnings);

        Assert.Equal(2, aggregated.Count);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddHours(4), aggregated[1].OpenTime);
        Assert.Single(warnings);
        Assert.Contains("incomplete", warnings[0]);
    }

    [Fact]
    public void QuoteVolumeIsUnknownRatherThanUnderstatedWhenAMemberLacksIt()
    {
        var bars = FifteenMinutes(16, quoteVolume: 10m);
        bars[7] = Candle.Of(
            CandleSource.BinanceFutures,
            "BTCUSDT",
            CandleInterval.FifteenMinutes,
            bars[7].OpenTime,
            bars[7].Open,
            bars[7].High,
            bars[7].Low,
            bars[7].Close,
            bars[7].Volume);

        var bar = Assert.Single(CandleAggregator.Aggregate(bars, CandleInterval.FourHours));

        Assert.Null(bar.QuoteVolume);
    }

    [Fact]
    public void AWeeklyBarOpensOnMondayMidnightUtc()
    {
        var bars = Daily(14, Monday);

        var aggregated = CandleAggregator.Aggregate(bars, CandleInterval.OneWeek);

        Assert.Equal(2, aggregated.Count);
        Assert.All(aggregated, b => Assert.Equal(DayOfWeek.Monday, b.OpenTime.DayOfWeek));
        Assert.Equal(Monday, aggregated[0].OpenTime);
        Assert.Equal(Monday.AddDays(7), aggregated[1].OpenTime);
    }

    [Fact]
    public void AggregatingToTheSourceIntervalReturnsTheSourceUntouched()
    {
        var bars = FifteenMinutes(16);

        Assert.Same(bars, CandleAggregator.Aggregate(bars, CandleInterval.FifteenMinutes));
    }

    [Fact]
    public void AnIntervalThatDoesNotDivideIsRefused()
    {
        var bars = Bars(8, CandleInterval.FourHours, DateTimeOffset.UnixEpoch);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => CandleAggregator.Aggregate(bars, CandleInterval.SixHours));
    }

    private static List<Candle> FifteenMinutes(int count, decimal? quoteVolume = null) =>
        Bars(count, CandleInterval.FifteenMinutes, DateTimeOffset.UnixEpoch, quoteVolume);

    private static List<Candle> Daily(int count, DateTimeOffset start) =>
        Bars(count, CandleInterval.OneDay, start);

    private static List<Candle> Bars(
        int count,
        CandleInterval interval,
        DateTimeOffset start,
        decimal? quoteVolume = null)
    {
        var candles = new List<Candle>(count);

        for (var i = 0; i < count; i++)
        {
            var mid = 100m + i;

            candles.Add(Candle.Of(
                CandleSource.BinanceFutures,
                "BTCUSDT",
                interval,
                start + interval.Duration() * i,
                mid,
                mid + 2m,
                mid - 2m,
                mid + 1m,
                volume: 10m,
                quoteVolume: quoteVolume));
        }

        return candles;
    }
}
