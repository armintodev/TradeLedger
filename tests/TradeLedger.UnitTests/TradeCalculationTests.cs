using TradeLedger.Core.Domain;

namespace TradeLedger.UnitTests;

public class TradeCalculationTests
{
    [Fact]
    public void Recalculate_NetIsGrossMinusFeesPlusFunding()
    {
        var trade = NewTrade();
        trade.GrossProfitLoss = 100m;
        trade.Fees = 3m;
        trade.Funding = -2m;

        trade.Recalculate();

        Assert.Equal(95m, trade.NetProfitLoss);
    }

    [Fact]
    public void Recalculate_PositiveFundingIsCredited()
    {
        var trade = NewTrade();
        trade.GrossProfitLoss = 100m;
        trade.Fees = 3m;
        trade.Funding = 5m;

        trade.Recalculate();

        Assert.Equal(102m, trade.NetProfitLoss);
    }

    [Fact]
    public void Recalculate_OutcomeFollowsNetNotGross()
    {
        var trade = NewTrade();
        trade.GrossProfitLoss = 5m;
        trade.Fees = 6m;
        trade.Funding = -1m;

        trade.Recalculate();

        Assert.Equal(-2m, trade.NetProfitLoss);
        Assert.Equal(TradeOutcome.Loss, trade.Outcome);
    }

    [Fact]
    public void Recalculate_OpenTradeHasNoOutcomeOrDuration()
    {
        var trade = NewTrade();
        trade.ClosedAt = null;
        trade.GrossProfitLoss = 50m;

        trade.Recalculate();

        Assert.Equal(TradeOutcome.Open, trade.Outcome);
        Assert.Null(trade.Duration);
    }

    [Fact]
    public void Recalculate_ZeroNetIsBreakeven()
    {
        var trade = NewTrade();
        trade.GrossProfitLoss = 3m;
        trade.Fees = 3m;
        trade.Funding = 0m;

        trade.Recalculate();

        Assert.Equal(TradeOutcome.Breakeven, trade.Outcome);
    }

    [Fact]
    public void Recalculate_AchievedRIsNetOverAmountRisked()
    {
        var trade = NewTrade();
        trade.EntryPrice = 100m;
        trade.StopLossPrice = 90m;
        trade.Quantity = 2m;
        trade.GrossProfitLoss = 60m;
        trade.Fees = 0m;

        trade.Recalculate();

        Assert.Equal(3m, trade.AchievedReturnR);
    }

    [Fact]
    public void Recalculate_AchievedRIsNullWithoutAStop()
    {
        var trade = NewTrade();
        trade.StopLossPrice = null;
        trade.GrossProfitLoss = 60m;

        trade.Recalculate();

        Assert.Null(trade.AchievedReturnR);
    }

    [Fact]
    public void Recalculate_DurationIsExitMinusEntry()
    {
        var trade = NewTrade();
        trade.OpenedAt = new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
        trade.ClosedAt = new DateTimeOffset(2026, 1, 1, 13, 30, 0, TimeSpan.Zero);

        trade.Recalculate();

        Assert.Equal(TimeSpan.FromHours(3.5), trade.Duration);
    }

    [Fact]
    public void Recalculate_AccountPercentagesUseBalanceBefore()
    {
        var trade = NewTrade();
        trade.GrossProfitLoss = 50m;
        trade.Fees = 0m;
        trade.Funding = 0m;

        trade.Recalculate(balanceBefore: 1000m);

        Assert.Equal(5m, trade.AccountChangePercent);
        Assert.Equal(1050m, trade.BalanceAfter);
    }

    [Fact]
    public void Recalculate_PlannedStopPercentIsDistanceOverEntry()
    {
        var trade = NewTrade();
        trade.EntryPrice = 200m;
        trade.StopLossPrice = 190m;

        trade.Recalculate();

        Assert.Equal(5m, trade.PlannedStopLossPercent);
    }

    [Fact]
    public void Recalculate_HandlesShortSideStopAboveEntry()
    {
        var trade = NewTrade();
        trade.Side = TradeSide.Short;
        trade.EntryPrice = 100m;
        trade.StopLossPrice = 110m;
        trade.Quantity = 1m;
        trade.GrossProfitLoss = 20m;
        trade.Fees = 0m;

        trade.Recalculate();

        Assert.Equal(2m, trade.AchievedReturnR);
        Assert.Equal(10m, trade.PlannedStopLossPercent);
    }

    private static Trade NewTrade() => new()
    {
        Symbol = "BTCUSDT",
        Side = TradeSide.Long,
        EntryPrice = 100m,
        Quantity = 1m,
        OpenedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        ClosedAt = new DateTimeOffset(2026, 1, 1, 1, 0, 0, TimeSpan.Zero),
    };
}
