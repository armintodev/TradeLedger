using TradeLedger.Core.Backtesting.Indicators;
using TradeLedger.Core.Backtesting.Rules;
using TradeLedger.Core.Domain.MarketData;
using TradeLedger.Core.MarketData;

namespace TradeLedger.UnitTests.Backtesting;

public class MultiTimeframeTests
{
    private const int Ratio = 16;

    [Fact]
    public void FourHoursIntoSixIsTheOnlyTradeablePairThatCannotAggregate()
    {
        var refused = new List<string>();

        foreach (var source in CandleIntervals.Tradeable)
        {
            foreach (var target in CandleIntervals.Tradeable)
            {
                if (target.Duration() >= source.Duration() && !source.DividesInto(target))
                {
                    refused.Add($"{source}->{target}");
                }
            }
        }

        Assert.Equal(["FourHours->SixHours"], refused);
    }

    [Fact]
    public void RatioToCountsBaseBarsPerHigherBar()
    {
        Assert.Equal(16, CandleInterval.FourHours.RatioTo(CandleInterval.FifteenMinutes));
        Assert.Equal(672, CandleInterval.OneWeek.RatioTo(CandleInterval.FifteenMinutes));
        Assert.Equal(1, CandleInterval.OneHour.RatioTo(CandleInterval.OneHour));
    }

    /// <summary>
    /// The projection guard. A bucket must become readable on the bar that closes it and not
    /// one bar sooner: sooner is lookahead, later is lag nobody asked for.
    /// </summary>
    [Fact]
    public void ABucketBecomesVisibleOnTheBarThatClosesItAndNotBefore()
    {
        var bars = FifteenMinutes(64);
        var buckets = CandleAggregator.Aggregate(bars, CandleInterval.FourHours);

        Assert.Equal(4, buckets.Count);

        var projected = IndicatorProjection.ProjectOntoBase(Ordinals(buckets.Count), buckets, bars);

        for (var i = 0; i < bars.Count; i++)
        {
            var bucket = i / Ratio;
            var closesItsBucket = i % Ratio == Ratio - 1;
            var expected = closesItsBucket ? bucket : bucket - 1;

            if (expected < 0)
            {
                Assert.Null(projected.At(i));
            }
            else
            {
                Assert.Equal((decimal?)expected, projected.At(i));
            }
        }
    }

    [Fact]
    public void ProjectionKeepsEveryOutputOfAMultiOutputIndicator()
    {
        var bars = FifteenMinutes(64);
        var buckets = CandleAggregator.Aggregate(bars, CandleInterval.FourHours);
        var computed = IndicatorFactory.Compute("Dmi", 2, PriceSource.Close, buckets);

        var projected = IndicatorProjection.ProjectOntoBase(computed, buckets, bars);

        Assert.True(projected.HasOutput(IndicatorFactory.PlusDi));
        Assert.True(projected.HasOutput(IndicatorFactory.MinusDi));
        Assert.Equal(bars.Count, projected.Length);
    }

    [Fact]
    public void AWrittenOffsetCountsInTheIndicatorsOwnBars()
    {
        var window = ProjectedWindow(out _);
        window.MoveTo(47);

        Assert.Equal(2m, window.Value(new IndicatorOperand("htf", null, 0)));
        Assert.Equal(1m, window.Value(new IndicatorOperand("htf", null, 1)));
        Assert.Equal(0m, window.Value(new IndicatorOperand("htf", null, 2)));
    }

    [Fact]
    public void RisingForWalksBucketsRatherThanBaseBars()
    {
        var trend = new TrendNode(
            RuleOperator.RisingFor, new IndicatorOperand("htf", null, 0), 2);

        var scaled = ProjectedWindow(out var unscaled);
        scaled.MoveTo(47);
        unscaled.MoveTo(47);

        Assert.True(RuleEvaluator.Evaluate(trend, scaled));

        // The regression guard. Walking base bars compares a projected value with itself for
        // every bar but the last of a bucket, so no monotonic run is ever found.
        Assert.False(RuleEvaluator.Evaluate(trend, unscaled));
    }

    [Fact]
    public void LoadFromGivesEveryIndicatorEnoughBucketsOnItsOwnGrid()
    {
        // The slow low-timeframe indicator wins the bar count while the fast daily one owns
        // the grid, which is the pairing a max-over-bar-counts reduction gets wrong.
        var document = Parse(
            """
            { "id": "slow", "type": "Ema", "params": { "period": 300 } },
            { "id": "daily", "type": "Sma", "interval": "OneDay", "params": { "period": 5 } }
            """);

        var from = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
        var loadFrom = document.LoadFrom(CandleInterval.FifteenMinutes, from);

        Assert.True(CompleteBuckets(CandleInterval.FifteenMinutes, loadFrom, from) >= 900);
        Assert.True(CompleteBuckets(CandleInterval.OneDay, loadFrom, from) >= 5);
    }

    [Fact]
    public void AStartUnalignedToTheIndicatorsGridStillWarmsFully()
    {
        var document = Parse(Adx4h);

        // 02:15 is a legal fifteen-minute start and sits a long way inside a four-hour bucket.
        var from = new DateTimeOffset(2026, 8, 1, 2, 15, 0, TimeSpan.Zero);
        var loadFrom = document.LoadFrom(CandleInterval.FifteenMinutes, from);

        Assert.True(CompleteBuckets(CandleInterval.FourHours, loadFrom, from) >= 70);
    }

    [Fact]
    public void WarmupInRunBarsScalesWithTheIntervalRatio()
    {
        var document = Parse(Adx4h);

        Assert.True(document.IsMultiTimeframe);
        Assert.Equal(70, document.WarmupBars);
        Assert.Equal((70 + 1) * 16, document.WarmupBarsFor(CandleInterval.FifteenMinutes));
    }

    private const string Adx4h =
        """{ "id": "adx", "type": "Adx", "interval": "FourHours", "params": { "period": 14 } }""";

    private static BarWindow ProjectedWindow(out BarWindow unscaled)
    {
        var bars = FifteenMinutes(64);
        var buckets = CandleAggregator.Aggregate(bars, CandleInterval.FourHours);
        var series = new Dictionary<string, IndicatorSeries>
        {
            ["htf"] = IndicatorProjection.ProjectOntoBase(Ordinals(buckets.Count), buckets, bars),
        };

        unscaled = new BarWindow(bars, series);

        return new BarWindow(bars, series, new Dictionary<string, int> { ["htf"] = Ratio });
    }

    /// <summary>A series whose value is its own bucket ordinal, so assertions read directly.</summary>
    private static IndicatorSeries Ordinals(int count)
    {
        var values = new decimal?[count];

        for (var i = 0; i < count; i++)
        {
            values[i] = i;
        }

        return IndicatorSeries.Single(values);
    }

    private static int CompleteBuckets(
        CandleInterval interval,
        DateTimeOffset loadFrom,
        DateTimeOffset from)
    {
        var duration = interval.Duration();
        var first = interval.IsAligned(loadFrom)
            ? loadFrom
            : interval.AlignFloor(loadFrom) + duration;

        return first >= from ? 0 : (int)((from - first).Ticks / duration.Ticks);
    }

    private static RuleDocument Parse(string indicators) => RuleDocumentParser.Parse($$"""
        {
          "version": 2,
          "indicators": [ {{indicators}} ],
          "entry": { "long": { "op": "GreaterThan", "left": { "price": "Close" }, "right": 0 } },
          "stopLoss": { "kind": "Percent", "percent": 2 }
        }
        """);

    private static List<Candle> FifteenMinutes(int count)
    {
        var candles = new List<Candle>(count);

        for (var i = 0; i < count; i++)
        {
            var mid = 100m + i;

            candles.Add(Candle.Of(
                CandleSource.BinanceFutures,
                "BTCUSDT",
                CandleInterval.FifteenMinutes,
                DateTimeOffset.UnixEpoch.AddMinutes(15 * i),
                mid,
                mid + 2m,
                mid - 2m,
                mid + 1m,
                volume: 10m));
        }

        return candles;
    }
}
