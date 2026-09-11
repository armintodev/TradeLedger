using TradeLedger.Core.Backtesting.Engine;
using TradeLedger.Core.Domain;

namespace TradeLedger.UnitTests.Backtesting;

public class PositionSizerTests
{
    [Fact]
    public void AStopOutLosesExactlyTheConfiguredPercentageOfEquity()
    {
        const decimal equity = 10_000m;
        const decimal riskPercent = 2m;

        var outcome = PositionSizer.Size(equity, riskPercent, TradeSide.Long, 100m, 98m, 5);

        Assert.True(outcome.Accepted);

        var size = outcome.Size!;
        var lossIfStopped = size.Quantity * size.StopDistance;

        Assert.Equal(equity * riskPercent / 100m, lossIfStopped);
        Assert.Equal(200m, lossIfStopped);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void EveryPermittedRiskLevelSizesToExactlyThatRisk(int riskPercent)
    {
        const decimal equity = 25_000m;

        var outcome = PositionSizer.Size(equity, riskPercent, TradeSide.Long, 250m, 245m, 10);

        var size = outcome.Size!;

        Assert.Equal(equity * riskPercent / 100m, size.Quantity * size.StopDistance);
    }

    [Fact]
    public void RiskIsTakenFromCurrentEquitySoItCompoundsAsTheAccountGrows()
    {
        var small = PositionSizer.Size(10_000m, 2m, TradeSide.Long, 100m, 98m, 5).Size!;
        var large = PositionSizer.Size(20_000m, 2m, TradeSide.Long, 100m, 98m, 5).Size!;

        Assert.Equal(small.Quantity * 2m, large.Quantity);
    }

    [Fact]
    public void ShortPositionsSizeFromTheStopAboveEntry()
    {
        var outcome = PositionSizer.Size(10_000m, 2m, TradeSide.Short, 100m, 102m, 5);

        Assert.True(outcome.Accepted);
        Assert.Equal(200m, outcome.Size!.Quantity * outcome.Size.StopDistance);
    }

    [Fact]
    public void MarginIsNotionalDividedByLeverage()
    {
        var outcome = PositionSizer.Size(10_000m, 2m, TradeSide.Long, 100m, 98m, 5);

        var size = outcome.Size!;

        Assert.Equal(size.Quantity * 100m, size.Notional);
        Assert.Equal(size.Notional / 5m, size.Margin);
    }

    [Fact]
    public void LeverageChangesMarginButNeverPositionSize()
    {
        var lowLeverage = PositionSizer.Size(10_000m, 2m, TradeSide.Long, 100m, 98m, 2).Size!;
        var highLeverage = PositionSizer.Size(10_000m, 2m, TradeSide.Long, 100m, 98m, 20).Size!;

        Assert.Equal(lowLeverage.Quantity, highLeverage.Quantity);
        Assert.True(highLeverage.Margin < lowLeverage.Margin);
    }

    [Fact]
    public void AStopOnTheWrongSideOfEntryIsRefusedForALong()
    {
        var outcome = PositionSizer.Size(10_000m, 2m, TradeSide.Long, 100m, 102m, 5);

        Assert.False(outcome.Accepted);
        Assert.Equal(SizingRefusal.InvalidStop, outcome.Refusal);
    }

    [Fact]
    public void AStopOnTheWrongSideOfEntryIsRefusedForAShort()
    {
        var outcome = PositionSizer.Size(10_000m, 2m, TradeSide.Short, 100m, 98m, 5);

        Assert.False(outcome.Accepted);
        Assert.Equal(SizingRefusal.InvalidStop, outcome.Refusal);
    }

    [Fact]
    public void AStopEqualToEntryIsRefusedRatherThanDividingByZero()
    {
        var outcome = PositionSizer.Size(10_000m, 2m, TradeSide.Long, 100m, 100m, 5);

        Assert.False(outcome.Accepted);
        Assert.Equal(SizingRefusal.InvalidStop, outcome.Refusal);
    }

    [Fact]
    public void APositionNeedingMoreMarginThanTheAccountHoldsIsRefusedNotShrunk()
    {
        var outcome = PositionSizer.Size(1_000m, 5m, TradeSide.Long, 100m, 99.99m, 1);

        Assert.False(outcome.Accepted);
        Assert.Equal(SizingRefusal.InsufficientMargin, outcome.Refusal);
    }

    [Fact]
    public void AnEmptyAccountIsRefused()
    {
        Assert.Equal(
            SizingRefusal.NoEquity,
            PositionSizer.Size(0m, 2m, TradeSide.Long, 100m, 98m, 5).Refusal);

        Assert.Equal(
            SizingRefusal.NoEquity,
            PositionSizer.Size(-50m, 2m, TradeSide.Long, 100m, 98m, 5).Refusal);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    public void TheTargetSitsExactlyTheRiskRewardMultipleAwayForALong(int ratio)
    {
        const decimal entry = 100m;
        const decimal stop = 98m;

        var target = PositionSizer.TakeProfitPrice(TradeSide.Long, entry, stop, ratio);

        Assert.Equal(Math.Abs(entry - stop) * ratio, Math.Abs(target - entry));
        Assert.True(target > entry);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    public void TheTargetSitsExactlyTheRiskRewardMultipleAwayForAShort(int ratio)
    {
        const decimal entry = 100m;
        const decimal stop = 102m;

        var target = PositionSizer.TakeProfitPrice(TradeSide.Short, entry, stop, ratio);

        Assert.Equal(Math.Abs(entry - stop) * ratio, Math.Abs(target - entry));
        Assert.True(target < entry);
    }

    [Fact]
    public void ATwoToOneTargetPaysTwiceWhatTheStopRisks()
    {
        const decimal equity = 10_000m;

        var size = PositionSizer.Size(equity, 2m, TradeSide.Long, 100m, 98m, 5).Size!;
        var target = PositionSizer.TakeProfitPrice(TradeSide.Long, 100m, 98m, 2m);

        var winBeforeCosts = CostModel.GrossProfitLoss(TradeSide.Long, 100m, target, size.Quantity);

        Assert.Equal(400m, winBeforeCosts);
        Assert.Equal(2m * size.Quantity * size.StopDistance, winBeforeCosts);
    }

    [Fact]
    public void SizingKeepsFullDecimalPrecisionOnSubSatoshiPrices()
    {
        var outcome = PositionSizer.Size(
            10_000m, 2m, TradeSide.Long, 0.000000012345678901m, 0.000000012000000000m, 5);

        Assert.True(outcome.Accepted);
        Assert.True(outcome.Size!.Quantity > 0);
    }
}
