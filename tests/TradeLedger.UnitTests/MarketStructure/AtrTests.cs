using TradeLedger.Core.Backtesting.Indicators;
using TradeLedger.Core.Domain.MarketData;

namespace TradeLedger.UnitTests.MarketStructure;

public class AtrTests
{
    [Fact]
    public void TrueRangeNeedsAPreviousBarSoTheSeedLandsOnThePeriod()
    {
        var candles = Sample();

        var atr = IndicatorMath.Atr(candles, 2);

        Assert.Null(atr[0]);
        Assert.Null(atr[1]);
        Assert.NotNull(atr[2]);
    }

    /// <summary>
    /// Worked by hand over <see cref="Sample"/>. True ranges are 4, 4 and 9: bar 1 and bar 2
    /// are both wider than their gap from the previous close, and bar 3 gaps nine away from it.
    /// The seed is their mean over the period, then Wilder's recursion takes over.
    /// </summary>
    [Fact]
    public void TheVectorMatchesWilderByHand()
    {
        var atr = IndicatorMath.Atr(Sample(), 2);

        Assert.Equal(4m, atr[2]);
        Assert.Equal(6.5m, atr[3]);
    }

    [Fact]
    public void ARangeNoLongerThanThePeriodIsAllNull()
    {
        var atr = IndicatorMath.Atr(Sample(), 4);

        Assert.All(atr, value => Assert.Null(value));
    }

    /// <summary>
    /// The recursion is the only part that is not obvious by reading, so it is checked against
    /// the plain loop it compresses — the same treatment the rolling-extreme deque gets.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(14)]
    [InlineData(50)]
    public void TheRecursionAgreesWithANaiveWilderLoop(int period)
    {
        var candles = Walk(400);

        var atr = IndicatorMath.Atr(candles, period);
        var naive = NaiveAtr(candles, period);

        for (var i = 0; i < candles.Count; i++)
        {
            Assert.Equal(naive[i], atr[i]);
        }
    }

    [Fact]
    public void ThePeriodMustBeAtLeastOne() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorMath.Atr(Sample(), 0));

    /// <summary>
    /// Wilder written out longhand: the mean of the first <paramref name="period"/> true ranges,
    /// then a running weighted average. Deliberately unoptimised.
    /// </summary>
    private static decimal?[] NaiveAtr(IReadOnlyList<Candle> candles, int period)
    {
        var result = new decimal?[candles.Count];

        if (candles.Count <= period)
        {
            return result;
        }

        var trueRanges = new decimal[candles.Count];

        for (var i = 1; i < candles.Count; i++)
        {
            var current = candles[i];
            var previousClose = candles[i - 1].Close;

            trueRanges[i] = new[]
            {
                current.High - current.Low,
                Math.Abs(current.High - previousClose),
                Math.Abs(current.Low - previousClose),
            }.Max();
        }

        var seed = 0m;

        for (var i = 1; i <= period; i++)
        {
            seed += trueRanges[i];
        }

        result[period] = seed / period;

        for (var i = period + 1; i < candles.Count; i++)
        {
            result[i] = ((result[i - 1] * (period - 1)) + trueRanges[i]) / period;
        }

        return result;
    }

    private static List<Candle> Sample() =>
    [
        Bar(0, open: 100m, high: 100m, low: 100m, close: 100m),
        Bar(1, open: 100m, high: 102m, low: 98m, close: 100m),
        Bar(2, open: 101m, high: 103m, low: 99m, close: 101m),
        Bar(3, open: 109m, high: 110m, low: 108m, close: 109m),
    ];

    private static List<Candle> Walk(int count)
    {
        var random = new Random(20260916);
        var candles = new List<Candle>(count);

        for (var i = 0; i < count; i++)
        {
            var mid = 1000m + Math.Round((decimal)(random.NextDouble() * 200), 6);
            var span = Math.Round((decimal)(random.NextDouble() * 8), 6) + 0.5m;

            candles.Add(Bar(i, open: mid, high: mid + span, low: mid - span, close: mid));
        }

        return candles;
    }

    private static Candle Bar(int index, decimal open, decimal high, decimal low, decimal close) =>
        Candle.Of(
            CandleSource.BinanceFutures,
            "BTCUSDT",
            CandleInterval.FifteenMinutes,
            DateTimeOffset.UnixEpoch.AddMinutes(15 * index),
            open,
            high,
            low,
            close,
            volume: 100m);
}
