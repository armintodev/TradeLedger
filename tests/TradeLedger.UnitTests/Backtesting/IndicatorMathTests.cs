using TradeLedger.Core.Backtesting.Indicators;
using TradeLedger.Core.Domain.MarketData;
using TradeLedger.Core.MarketData;

namespace TradeLedger.UnitTests.Backtesting;

public class IndicatorMathTests
{
    private static readonly decimal[] WilderCloses =
    [
        44.3389m, 44.0902m, 44.1497m, 43.6124m, 44.3278m, 44.8264m, 45.0955m,
        45.4245m, 45.8433m, 46.0826m, 45.8931m, 46.0328m, 45.6140m, 46.2820m,
        46.2820m, 46.0028m, 46.0328m, 46.4116m, 46.2222m, 45.6439m, 46.2122m,
        46.2521m, 45.7137m, 46.4515m, 45.7835m, 45.3548m, 44.0288m, 44.1783m,
        44.2181m, 44.5714m, 43.4205m, 42.6628m, 43.1314m,
    ];

    private static readonly decimal[] PublishedRsi14 =
    [
        70.53m, 66.32m, 66.55m, 69.41m, 66.36m, 57.97m, 62.93m, 63.26m, 56.06m,
        62.38m, 54.71m, 50.42m, 39.99m, 41.46m, 41.87m, 45.46m, 37.30m, 33.08m,
        37.79m,
    ];

    [Fact]
    public void RsiMatchesWildersPublishedFourteenPeriodVector()
    {
        var rsi = IndicatorMath.Rsi(WilderCloses, 14);

        for (var i = 0; i < PublishedRsi14.Length; i++)
        {
            var actual = rsi[14 + i];

            Assert.NotNull(actual);
            Assert.True(
                Math.Abs(actual.Value - PublishedRsi14[i]) <= 0.05m,
                $"RSI at bar {14 + i} was {actual} but the published value is {PublishedRsi14[i]}.");
        }
    }

    [Fact]
    public void TheFirstRsiValueMatchesWildersFormulaComputedFromScratch()
    {
        var gains = 0m;
        var losses = 0m;

        for (var i = 1; i <= 14; i++)
        {
            var change = WilderCloses[i] - WilderCloses[i - 1];

            if (change > 0)
            {
                gains += change;
            }
            else
            {
                losses += -change;
            }
        }

        var expected = 100m - 100m / (1m + gains / 14m / (losses / 14m));

        Assert.Equal(expected, IndicatorMath.Rsi(WilderCloses, 14)[14]);
    }

    [Fact]
    public void RsiIsNullUntilExactlyOnePeriodOfChangesExists()
    {
        var rsi = IndicatorMath.Rsi(WilderCloses, 14);

        for (var i = 0; i < 14; i++)
        {
            Assert.Null(rsi[i]);
        }

        Assert.NotNull(rsi[14]);
    }

    [Fact]
    public void RsiUsesWilderSmoothingNotARollingSimpleAverage()
    {
        var wilder = IndicatorMath.Rsi(WilderCloses, 14);
        var rolling = SimpleAverageRsi(WilderCloses, 14);

        var biggestDivergence = 0m;

        for (var i = 14; i < WilderCloses.Length; i++)
        {
            biggestDivergence = Math.Max(
                biggestDivergence,
                Math.Abs(wilder[i]!.Value - rolling[i]!.Value));
        }

        Assert.True(
            biggestDivergence > 1m,
            "Wilder smoothing and a rolling simple average must diverge materially on this " +
            $"data, but the largest difference was only {biggestDivergence}. A near-identical " +
            "result means the implementation is averaging rather than smoothing.");

        Assert.True(Math.Abs(wilder[20]!.Value - PublishedRsi14[6]) <= 0.05m);
        Assert.True(Math.Abs(rolling[17]!.Value - PublishedRsi14[3]) > 1m);
    }

    [Fact]
    public void RsiIsOneHundredWhenNothingEverFalls()
    {
        decimal[] rising = [.. Enumerable.Range(0, 30).Select(i => 100m + i)];

        var rsi = IndicatorMath.Rsi(rising, 14);

        Assert.Equal(100m, rsi[14]);
        Assert.Equal(100m, rsi[^1]);
    }

    [Fact]
    public void RsiIsZeroWhenNothingEverRises()
    {
        decimal[] falling = [.. Enumerable.Range(0, 30).Select(i => 100m - i)];

        var rsi = IndicatorMath.Rsi(falling, 14);

        Assert.Equal(0m, rsi[14]);
    }

    [Fact]
    public void RsiIsFiftyOnACompletelyFlatSeries()
    {
        decimal[] flat = [.. Enumerable.Repeat(100m, 30)];

        Assert.Equal(50m, IndicatorMath.Rsi(flat, 14)[14]);
    }

    [Fact]
    public void RsiStaysWithinItsBounds()
    {
        var rsi = IndicatorMath.Rsi(WilderCloses, 14);

        foreach (var value in rsi.Where(v => v is not null))
        {
            Assert.InRange(value!.Value, 0m, 100m);
        }
    }

    [Fact]
    public void SmaAveragesTheWindowAndStartsAtThePeriodBoundary()
    {
        decimal[] values = [2m, 4m, 6m, 8m, 10m];

        var sma = IndicatorMath.Sma(values, 2);

        Assert.Null(sma[0]);
        Assert.Equal(3m, sma[1]);
        Assert.Equal(5m, sma[2]);
        Assert.Equal(7m, sma[3]);
        Assert.Equal(9m, sma[4]);
    }

    [Fact]
    public void SmaHandlesAPeriodOfOneAsThePriceItself()
    {
        decimal[] values = [2m, 4m, 6m];

        Assert.Equal([2m, 4m, 6m], IndicatorMath.Sma(values, 1).Select(v => v!.Value));
    }

    [Fact]
    public void SmaIsAllNullWhenThereIsNotEnoughData()
    {
        Assert.All(IndicatorMath.Sma([1m, 2m], 5), Assert.Null);
    }

    [Fact]
    public void EmaSeedsWithASimpleAverageThenAppliesTheMultiplier()
    {
        decimal[] values = [1m, 2m, 3m, 4m, 5m];

        var ema = IndicatorMath.Ema(values, 3);

        Assert.Null(ema[0]);
        Assert.Null(ema[1]);
        Assert.Equal(2m, ema[2]);
        Assert.Equal(3m, ema[3]);
        Assert.Equal(4m, ema[4]);
    }

    [Fact]
    public void EmaReactsFasterThanSmaToARecentJump()
    {
        decimal[] values = [10m, 10m, 10m, 10m, 10m, 10m, 10m, 10m, 10m, 20m];

        var ema = IndicatorMath.Ema(values, 5);
        var sma = IndicatorMath.Sma(values, 5);

        Assert.True(ema[^1] > sma[^1]);
    }

    [Fact]
    public void EmaKeepsFullDecimalPrecision()
    {
        decimal[] values = [0.000000012345678901m, 0.000000012345678902m, 0.000000012345678903m];

        var ema = IndicatorMath.Ema(values, 3);

        Assert.NotNull(ema[2]);
        Assert.True(ema[2]!.Value > 0.0000000123456789m);
    }

    [Fact]
    public void DirectionalIndicatorsFavourTheUpsideInAnUptrend()
    {
        var candles = Trend(40, rising: true);

        var (plus, minus, _) = IndicatorMath.Dmi(candles, 14);

        Assert.NotNull(plus[^1]);
        Assert.NotNull(minus[^1]);
        Assert.True(plus[^1] > minus[^1]);
    }

    [Fact]
    public void DirectionalIndicatorsFavourTheDownsideInADowntrend()
    {
        var candles = Trend(40, rising: false);

        var (plus, minus, _) = IndicatorMath.Dmi(candles, 14);

        Assert.True(minus[^1] > plus[^1]);
    }

    [Fact]
    public void DirectionalIndicatorsAreNullUntilOnePeriodOfMovementExists()
    {
        var candles = Trend(40, rising: true);

        var (plus, minus, dx) = IndicatorMath.Dmi(candles, 14);

        for (var i = 0; i < 14; i++)
        {
            Assert.Null(plus[i]);
            Assert.Null(minus[i]);
            Assert.Null(dx[i]);
        }

        Assert.NotNull(plus[14]);
        Assert.NotNull(dx[14]);
    }

    [Fact]
    public void DirectionalIndicatorsStayWithinTheirBounds()
    {
        var candles = Choppy(120);

        var (plus, minus, dx) = IndicatorMath.Dmi(candles, 14);

        foreach (var value in plus.Concat(minus).Concat(dx).Where(v => v is not null))
        {
            Assert.InRange(value!.Value, 0m, 100m);
        }
    }

    [Fact]
    public void AFlatMarketProducesZeroDirectionalMovementRatherThanDividingByZero()
    {
        var candles = Enumerable.Range(0, 40)
            .Select(i => Bar(i, 100m, 100m, 100m, 100m))
            .ToList();

        var (plus, minus, dx) = IndicatorMath.Dmi(candles, 14);

        Assert.Equal(0m, plus[14]);
        Assert.Equal(0m, minus[14]);
        Assert.Equal(0m, dx[14]);
        Assert.Equal(0m, IndicatorMath.Adx(candles, 14)[27]);
    }

    [Fact]
    public void AdxFirstAppearsAtTwiceThePeriodMinusOne()
    {
        var candles = Trend(80, rising: true);
        const int period = 14;

        var adx = IndicatorMath.Adx(candles, period);

        for (var i = 0; i < 2 * period - 1; i++)
        {
            Assert.Null(adx[i]);
        }

        Assert.NotNull(adx[2 * period - 1]);
    }

    [Fact]
    public void AdxSeedsWithTheSimpleAverageOfTheFirstPeriodOfDx()
    {
        var candles = Choppy(120);
        const int period = 14;

        var (_, _, dx) = IndicatorMath.Dmi(candles, period);
        var adx = IndicatorMath.Adx(candles, period);

        var expected = Enumerable
            .Range(period, period)
            .Select(i => dx[i]!.Value)
            .Sum() / period;

        Assert.Equal(expected, adx[2 * period - 1]);
    }

    [Fact]
    public void AdxRisesWhenChopGivesWayToATrend()
    {
        var candles = Choppy(60);
        var trend = Trend(60, rising: true);

        var offset = candles[^1].Close;

        for (var i = 0; i < trend.Count; i++)
        {
            candles.Add(Bar(
                60 + i,
                trend[i].Open + offset,
                trend[i].High + offset,
                trend[i].Low + offset,
                trend[i].Close + offset));
        }

        var adx = IndicatorMath.Adx(candles, 14);

        Assert.NotNull(adx[59]);
        Assert.NotNull(adx[^1]);
        Assert.True(
            adx[^1] > adx[59],
            $"ADX should climb once a trend takes hold, but went from {adx[59]} to {adx[^1]}.");
    }

    [Fact]
    public void AdxSaturatesRatherThanExceedingOneHundredInAPerfectTrend()
    {
        var adx = IndicatorMath.Adx(Trend(120, rising: true), 14);

        Assert.Equal(100m, adx[^1]);
    }

    [Fact]
    public void AdxStaysWithinItsBounds()
    {
        var adx = IndicatorMath.Adx(Choppy(200), 14);

        foreach (var value in adx.Where(v => v is not null))
        {
            Assert.InRange(value!.Value, 0m, 100m);
        }
    }

    [Fact]
    public void TrueRangeAccountsForGapsAcrossTheClose()
    {
        List<Candle> candles =
        [
            Bar(0, 100m, 101m, 99m, 100m),
            Bar(1, 120m, 121m, 119m, 120m),
            Bar(2, 121m, 122m, 120m, 121m),
        ];

        var (_, _, _) = IndicatorMath.Dmi(candles, 1);

        var (plus, _, _) = IndicatorMath.Dmi(candles, 1);

        Assert.NotNull(plus[1]);
        Assert.True(plus[1] > 0m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void APeriodBelowOneIsRejected(int period)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorMath.Sma([1m, 2m], period));
        Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorMath.Ema([1m, 2m], period));
        Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorMath.Rsi([1m, 2m], period));
    }

    private static decimal?[] SimpleAverageRsi(decimal[] values, int period)
    {
        var result = new decimal?[values.Length];

        for (var i = period; i < values.Length; i++)
        {
            var gain = 0m;
            var loss = 0m;

            for (var j = i - period + 1; j <= i; j++)
            {
                var change = values[j] - values[j - 1];

                if (change > 0)
                {
                    gain += change;
                }
                else
                {
                    loss += -change;
                }
            }

            var averageGain = gain / period;
            var averageLoss = loss / period;

            result[i] = averageLoss == 0
                ? 100m
                : 100m - 100m / (1m + averageGain / averageLoss);
        }

        return result;
    }

    private static List<Candle> Trend(int count, bool rising)
    {
        var candles = new List<Candle>(count);

        for (var i = 0; i < count; i++)
        {
            var basePrice = rising ? 100m + i * 2m : 100m + (count - i) * 2m;

            candles.Add(Bar(i, basePrice, basePrice + 1.5m, basePrice - 0.5m, basePrice + 1m));
        }

        return candles;
    }

    private static List<Candle> Choppy(int count)
    {
        var candles = new List<Candle>(count);
        var random = new Random(20260911);
        var price = 100m;

        for (var i = 0; i < count; i++)
        {
            price += (decimal)(random.NextDouble() * 4 - 2);
            price = Math.Max(1m, price);

            var high = price + (decimal)(random.NextDouble() * 2);
            var low = price - (decimal)(random.NextDouble() * 2);
            low = Math.Max(0.5m, Math.Min(low, price));

            candles.Add(Bar(i, price, Math.Max(high, price), low, price));
        }

        return candles;
    }

    private static Candle Bar(int index, decimal open, decimal high, decimal low, decimal close) =>
        Candle.Of(
            CandleSource.BinanceFutures,
            "BTCUSDT",
            CandleInterval.OneHour,
            DateTimeOffset.UnixEpoch.AddHours(index),
            open,
            high,
            low,
            close,
            volume: 1000m);
}
