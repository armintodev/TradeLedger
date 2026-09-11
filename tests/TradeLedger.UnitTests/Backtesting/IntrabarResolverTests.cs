using TradeLedger.Core.Backtesting.Engine;
using TradeLedger.Core.Domain;
using TradeLedger.Core.Domain.Backtesting;
using TradeLedger.Core.Domain.MarketData;
using TradeLedger.Core.MarketData;

namespace TradeLedger.UnitTests.Backtesting;

public class IntrabarResolverTests
{
    private static readonly IntrabarLevels LongLevels =
        new(Stop: 98m, Target: 104m, Liquidation: 96m);

    private static readonly IntrabarLevels ShortLevels =
        new(Stop: 102m, Target: 96m, Liquidation: 104m);

    [Fact]
    public void ABarThatReachesNothingClosesNoTrade()
    {
        Assert.Null(Resolve(Bar(low: 99m, high: 103m)));
    }

    [Fact]
    public void ABarThatReachesOnlyTheStopNeedsNoDrillDown()
    {
        var outcome = Resolve(Bar(low: 97m, high: 101m));

        Assert.NotNull(outcome);
        Assert.Equal(IntrabarLevel.Stop, outcome.Level);
        Assert.Equal(IntrabarResolution.Unambiguous, outcome.Resolution);
    }

    [Fact]
    public void ABarThatReachesOnlyTheTargetNeedsNoDrillDown()
    {
        var outcome = Resolve(Bar(low: 99m, high: 105m));

        Assert.NotNull(outcome);
        Assert.Equal(IntrabarLevel.Target, outcome.Level);
        Assert.Equal(IntrabarResolution.Unambiguous, outcome.Resolution);
    }

    [Fact]
    public void ABarThatGapsClearPastTheStopStillStopsOut()
    {
        var gapped = Bar(low: 80m, high: 90m, open: 85m);

        var outcome = Resolve(gapped);

        Assert.NotNull(outcome);
        Assert.Equal(IntrabarLevel.Liquidation, outcome.Level);
    }

    [Fact]
    public void AStopGappedPastFillsAtTheOpenNotAtTheStop()
    {
        var gapped = Bar(low: 90m, high: 95m, open: 94m);
        var levels = new IntrabarLevels(Stop: 98m, Target: 104m, Liquidation: 80m);

        var outcome = IntrabarResolver.Resolve(gapped, TradeSide.Long, levels);

        Assert.Equal(IntrabarLevel.Stop, outcome!.Level);

        var fill = IntrabarResolver.FillPrice(IntrabarLevel.Stop, TradeSide.Long, levels, gapped);

        Assert.Equal(94m, fill);
        Assert.True(fill < levels.Stop);
    }

    [Fact]
    public void AStopReachedNormallyFillsAtTheStop()
    {
        var bar = Bar(low: 97m, high: 101m, open: 100m);

        var fill = IntrabarResolver.FillPrice(IntrabarLevel.Stop, TradeSide.Long, LongLevels, bar);

        Assert.Equal(98m, fill);
    }

    [Fact]
    public void AShortStopGappedPastFillsAtTheOpenAbove()
    {
        var gapped = Bar(low: 108m, high: 112m, open: 109m);
        var levels = new IntrabarLevels(Stop: 102m, Target: 96m, Liquidation: 130m);

        var outcome = IntrabarResolver.Resolve(gapped, TradeSide.Short, levels);

        Assert.Equal(IntrabarLevel.Stop, outcome!.Level);

        Assert.Equal(
            109m,
            IntrabarResolver.FillPrice(IntrabarLevel.Stop, TradeSide.Short, levels, gapped));
    }

    [Fact]
    public void ATargetGappedPastStillFillsAtTheTargetRatherThanFlatteringTheResult()
    {
        var gapped = Bar(low: 110m, high: 115m, open: 112m);

        var fill = IntrabarResolver.FillPrice(IntrabarLevel.Target, TradeSide.Long, LongLevels, gapped);

        Assert.Equal(104m, fill);
    }

    [Fact]
    public void AnAmbiguousBarWithoutMinuteDataAssumesTheStopFilledFirst()
    {
        var outcome = Resolve(Bar(low: 97m, high: 105m));

        Assert.NotNull(outcome);
        Assert.Equal(IntrabarLevel.Stop, outcome.Level);
        Assert.Equal(IntrabarResolution.AssumedNoMinuteData, outcome.Resolution);
    }

    [Fact]
    public void MinuteDataResolvesInFavourOfTheTargetWhenItCameFirst()
    {
        List<Candle> minutes = [Minute(0, 99m, 105m), Minute(1, 97m, 100m)];

        var outcome = Resolve(Bar(low: 97m, high: 105m), minutes);

        Assert.Equal(IntrabarLevel.Target, outcome!.Level);
        Assert.Equal(IntrabarResolution.ResolvedByMinute, outcome.Resolution);
    }

    [Fact]
    public void MinuteDataResolvesInFavourOfTheStopWhenItCameFirst()
    {
        List<Candle> minutes = [Minute(0, 97m, 100m), Minute(1, 99m, 105m)];

        var outcome = Resolve(Bar(low: 97m, high: 105m), minutes);

        Assert.Equal(IntrabarLevel.Stop, outcome!.Level);
        Assert.Equal(IntrabarResolution.ResolvedByMinute, outcome.Resolution);
    }

    [Fact]
    public void MinuteBarsAreWalkedInTimeOrderNotListOrder()
    {
        List<Candle> minutes = [Minute(5, 99m, 105m), Minute(1, 97m, 100m)];

        Assert.Equal(IntrabarLevel.Stop, Resolve(Bar(low: 97m, high: 105m), minutes)!.Level);
    }

    [Fact]
    public void MinutesThatReachNothingAreSkippedRatherThanEndingTheSearch()
    {
        List<Candle> minutes =
        [
            Minute(0, 99m, 100m),
            Minute(1, 99.5m, 100.5m),
            Minute(2, 99m, 105m),
        ];

        var outcome = Resolve(Bar(low: 97m, high: 105m), minutes);

        Assert.Equal(IntrabarLevel.Target, outcome!.Level);
        Assert.Equal(IntrabarResolution.ResolvedByMinute, outcome.Resolution);
    }

    [Fact]
    public void OneMinuteHoldingBothLevelsFallsBackToThePessimisticAssumption()
    {
        List<Candle> minutes = [Minute(0, 97m, 105m)];

        var outcome = Resolve(Bar(low: 97m, high: 105m), minutes);

        Assert.Equal(IntrabarLevel.Stop, outcome!.Level);
        Assert.Equal(IntrabarResolution.AssumedWithinMinute, outcome.Resolution);
    }

    [Fact]
    public void MinuteDataThatMissesTheWindowEntirelyDegradesToTheAssumption()
    {
        List<Candle> minutes = [Minute(0, 99m, 100m), Minute(1, 99.5m, 100.5m)];

        var outcome = Resolve(Bar(low: 97m, high: 105m), minutes);

        Assert.Equal(IntrabarLevel.Stop, outcome!.Level);
        Assert.Equal(IntrabarResolution.AssumedNoMinuteData, outcome.Resolution);
    }

    [Fact]
    public void LiquidationBeatsTheStopWhenBothAreReached()
    {
        Assert.Equal(IntrabarLevel.Liquidation, Resolve(Bar(low: 95m, high: 101m))!.Level);
    }

    [Fact]
    public void LiquidationBeatsEverythingWhenTheWholeRangeIsSwept()
    {
        var outcome = Resolve(Bar(low: 95m, high: 105m));

        Assert.Equal(IntrabarLevel.Liquidation, outcome!.Level);
        Assert.Equal(IntrabarResolution.AssumedNoMinuteData, outcome.Resolution);
    }

    [Fact]
    public void AMinuteCanStillRescueATargetAheadOfALiquidation()
    {
        List<Candle> minutes = [Minute(0, 99m, 105m), Minute(1, 95m, 100m)];

        var outcome = Resolve(Bar(low: 95m, high: 105m), minutes);

        Assert.Equal(IntrabarLevel.Target, outcome!.Level);
        Assert.Equal(IntrabarResolution.ResolvedByMinute, outcome.Resolution);
    }

    [Fact]
    public void TouchingALevelExactlyAtTheBarBoundaryCounts()
    {
        Assert.Equal(IntrabarLevel.Target, Resolve(Bar(low: 99m, high: 104m))!.Level);
        Assert.Equal(IntrabarLevel.Stop, Resolve(Bar(low: 98m, high: 100m))!.Level);
    }

    [Fact]
    public void ShortPositionsResolveWithInvertedLevels()
    {
        var stopped = IntrabarResolver.Resolve(Bar(low: 99m, high: 103m), TradeSide.Short, ShortLevels);
        var targeted = IntrabarResolver.Resolve(Bar(low: 95m, high: 101m), TradeSide.Short, ShortLevels);
        var ambiguous = IntrabarResolver.Resolve(Bar(low: 95m, high: 103m), TradeSide.Short, ShortLevels);

        Assert.Equal(IntrabarLevel.Stop, stopped!.Level);
        Assert.Equal(IntrabarLevel.Target, targeted!.Level);
        Assert.Equal(IntrabarLevel.Stop, ambiguous!.Level);
        Assert.Equal(IntrabarResolution.AssumedNoMinuteData, ambiguous.Resolution);
    }

    [Fact]
    public void AShortIsUntouchedByABarBelowEveryLevel()
    {
        Assert.Null(
            IntrabarResolver.Resolve(Bar(low: 97m, high: 101m), TradeSide.Short, ShortLevels));
    }

    [Theory]
    [InlineData(IntrabarLevel.Stop, BacktestExitReason.StopLoss)]
    [InlineData(IntrabarLevel.Target, BacktestExitReason.TakeProfit)]
    [InlineData(IntrabarLevel.Liquidation, BacktestExitReason.Liquidation)]
    public void EachLevelMapsToItsExitReason(IntrabarLevel level, BacktestExitReason expected)
    {
        Assert.Equal(expected, level.ToExitReason());
    }

    [Fact]
    public void PriceForReturnsTheLevelItself()
    {
        Assert.Equal(98m, IntrabarResolver.PriceFor(IntrabarLevel.Stop, LongLevels));
        Assert.Equal(104m, IntrabarResolver.PriceFor(IntrabarLevel.Target, LongLevels));
        Assert.Equal(96m, IntrabarResolver.PriceFor(IntrabarLevel.Liquidation, LongLevels));
    }

    private static IntrabarOutcome? Resolve(Candle bar, IReadOnlyList<Candle>? minutes = null) =>
        IntrabarResolver.Resolve(bar, TradeSide.Long, LongLevels, minutes);

    private static Candle Bar(decimal low, decimal high, decimal? open = null) => Candle.Of(
        CandleSource.BinanceFutures,
        "BTCUSDT",
        CandleInterval.FifteenMinutes,
        DateTimeOffset.UnixEpoch,
        open ?? low,
        high,
        low,
        high,
        volume: 1000m);

    private static Candle Minute(int offset, decimal low, decimal high) => Candle.Of(
        CandleSource.BinanceFutures,
        "BTCUSDT",
        CandleInterval.OneMinute,
        DateTimeOffset.UnixEpoch.AddMinutes(offset),
        low,
        high,
        low,
        high,
        volume: 100m);
}
