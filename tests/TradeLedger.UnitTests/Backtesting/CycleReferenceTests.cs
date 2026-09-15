using TradeLedger.Core.Backtesting;
using TradeLedger.Core.Backtesting.Engine;
using TradeLedger.Core.Backtesting.Indicators;
using TradeLedger.Core.Backtesting.Rules;
using TradeLedger.Core.Domain.Backtesting;
using TradeLedger.Core.Domain.MarketData;
using TradeLedger.Core.MarketData;

namespace TradeLedger.UnitTests.Backtesting;

public class CycleReferenceTests
{
    private const string CrossAboveSma = """
        {
          "version": 1,
          "indicators": [ { "id": "fast", "type": "Sma", "params": { "period": 5 } } ],
          "entry": {
            "long": {
              "op": "CrossesAbove",
              "left": { "price": "Close" },
              "right": { "ref": "fast" }
            }
          },
          "stopLoss": { "kind": "Percent", "percent": 3 }
        }
        """;

    [Theory]
    [InlineData(CandleInterval.FifteenMinutes, CandleInterval.FourHours)]
    [InlineData(CandleInterval.OneHour, CandleInterval.FourHours)]
    [InlineData(CandleInterval.FourHours, CandleInterval.FourHours)]
    public void ARunAtOrBelowFourHoursIsMeasuredOnFourHourBars(
        CandleInterval run,
        CandleInterval expected) =>
        Assert.Equal(expected, CycleReference.IntervalFor(run));

    /// <summary>
    /// Six-hour bars are the pair that cannot be built from four-hour ones, and a daily or
    /// weekly run already trades coarser than the reference. Both fall back to the run's own
    /// interval rather than to something the candles cannot produce.
    /// </summary>
    [Theory]
    [InlineData(CandleInterval.SixHours)]
    [InlineData(CandleInterval.OneDay)]
    [InlineData(CandleInterval.OneWeek)]
    public void ACoarserRunIsMeasuredOnItsOwnBars(CandleInterval run) =>
        Assert.Equal(run, CycleReference.IntervalFor(run));

    /// <summary>
    /// The one that matters. A position is opened at the next bar's open, but the market state
    /// that caused it is the one the entry condition saw — so the reading comes from the signal
    /// bar. Reading the fill bar instead would attribute a trade to the cycle it ran into
    /// rather than the cycle it was taken in.
    /// </summary>
    [Fact]
    public async Task TheReadingIsTakenAtTheSignalBarNotTheFillBar()
    {
        // Daily bars, so the reference is the run's own interval and the series moves every
        // bar. On a projected higher timeframe it is flat for most of a bucket, which would
        // let a fill-bar read pass unnoticed.
        var bars = Daily(200);
        var result = await RunAsync(bars);
        var adx = IndicatorMath.Adx(bars, CycleReference.Period);

        var warm = result.Trades.Where(t => t.CycleAdx is not null).ToList();

        Assert.NotEmpty(warm);
        Assert.All(warm, t => Assert.Equal(adx[t.EntryBarIndex - 1], t.CycleAdx));

        // Without this the assertion above would also hold for a fill-bar read.
        Assert.Contains(warm, t => adx[t.EntryBarIndex] != adx[t.EntryBarIndex - 1]);
    }

    [Fact]
    public async Task EveryPositionRecordsTheIntervalItWasMeasuredOn()
    {
        var result = await RunAsync(Daily(200));

        Assert.NotEmpty(result.Trades);
        Assert.All(result.Trades, t => Assert.Equal(CandleInterval.OneDay, t.CycleInterval));
    }

    /// <summary>
    /// The regression guard. The reference is an extra indicator the strategy never declared,
    /// and ADX needs far more history than most rules do. If it reached the warmth calculation
    /// it would push back the first tradeable bar of every existing strategy — silently, since
    /// the result would still be a clean run with fewer trades in it.
    /// </summary>
    [Fact]
    public async Task AnUnwarmReadingDoesNotHoldBackTheRun()
    {
        // Twenty bars reach the first crossover and leave ADX(14) still null everywhere:
        // Wilder's smoothing produces nothing until roughly twice its period.
        var result = await RunAsync(Daily(20));

        Assert.NotEmpty(result.Trades);
        Assert.All(result.Trades, t => Assert.Null(t.CycleAdx));

        // The interval is still recorded: the row says what was attempted, not just what landed.
        Assert.All(result.Trades, t => Assert.Equal(CandleInterval.OneDay, t.CycleInterval));
    }

    [Fact]
    public async Task AnHourlyRunIsMeasuredOnAggregatedFourHourBars()
    {
        var result = await RunAsync(Hourly(400));

        Assert.NotEmpty(result.Trades);
        Assert.All(result.Trades, t => Assert.Equal(CandleInterval.FourHours, t.CycleInterval));
        Assert.Contains(result.Trades, t => t.CycleAdx is not null);
    }

    private static async Task<BacktestEngineResult> RunAsync(List<Candle> bars)
    {
        var parameters = new SimulationParameters(
            Symbol: "BTCUSDT",
            From: bars[0].OpenTime,
            OpeningBalance: 10_000m,
            RiskPercentPerPosition: 1m,
            RiskRewardRatio: 2m,
            Leverage: 5,
            MaintenanceMarginRate: 0.005m,
            Costs: new CostModel(0m, 0m, 0m));

        return await RuleSimulationCore.RunAsync(
            RuleDocumentParser.Parse(CrossAboveSma),
            bars,
            startIndex: 0,
            parameters,
            funding: [],
            NoMinuteCandles.Instance,
            NoProgress.Instance,
            warnings: []);
    }

    private static List<Candle> Daily(int count) =>
        Build(count, CandleInterval.OneDay, i => DateTimeOffset.UnixEpoch.AddDays(i));

    private static List<Candle> Hourly(int count) =>
        Build(count, CandleInterval.OneHour, i => DateTimeOffset.UnixEpoch.AddHours(i));

    /// <summary>
    /// A deterministic zig-zag — twelve bars down, twelve up — so ADX both builds and decays
    /// across the series and a five-bar SMA is crossed repeatedly in both directions. It falls
    /// first so that the earliest crossover lands around bar 13, well before ADX(14) has a
    /// value at all: that gap is what the unwarm test stands on.
    /// </summary>
    private static List<Candle> Build(
        int count,
        CandleInterval interval,
        Func<int, DateTimeOffset> openTime)
    {
        var candles = new List<Candle>(count);
        var close = 100m;

        for (var i = 0; i < count; i++)
        {
            var open = close;

            close = open + (i % 24 < 12 ? -2m : 3m);

            candles.Add(Candle.Of(
                CandleSource.BinanceFutures,
                "BTCUSDT",
                interval,
                openTime(i),
                open,
                Math.Max(open, close) + 1m,
                Math.Min(open, close) - 1m,
                close,
                volume: 1000m));
        }

        return candles;
    }
}
