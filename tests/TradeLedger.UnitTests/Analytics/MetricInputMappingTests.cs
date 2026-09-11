using TradeLedger.Core.Analytics;
using TradeLedger.Core.Domain;

namespace TradeLedger.UnitTests.Analytics;

public class MetricInputMappingTests
{
    [Fact]
    public void EveryFieldTheMetricsNeedIsCarriedOverFromTheTrade()
    {
        var opened = new DateTimeOffset(2026, 1, 5, 13, 0, 0, TimeSpan.Zero);

        var trade = Trade.OpenManual(new NewManualTrade
        {
            AccountId = Guid.CreateVersion7(),
            Symbol = "BTCUSDT",
            Side = TradeSide.Long,
            EntryPrice = 100m,
            Quantity = 2m,
            StopLossPrice = 90m,
            OpenedAt = opened,
            ClosedAt = opened.AddHours(4),
            ExitPrice = 130m,
            Fees = 3m,
            Funding = -1m,
        });

        var input = AnalyticsService.ToMetricInput(trade);

        Assert.Equal(trade.OpenedAt, input.OpenedAt);
        Assert.Equal(trade.ClosedAt, input.ClosedAt);
        Assert.Equal(trade.GrossProfitLoss, input.GrossProfitLoss);
        Assert.Equal(trade.Fees, input.Fees);
        Assert.Equal(trade.Funding, input.Funding);
        Assert.Equal(trade.NetProfitLoss, input.NetProfitLoss);
        Assert.Equal(trade.Outcome, input.Outcome);
        Assert.Equal(trade.AchievedReturnR, input.AchievedReturnR);
        Assert.Equal(trade.PlannedReturnR, input.PlannedReturnR);
        Assert.Equal(trade.IsPlanned, input.IsPlanned);
        Assert.Equal(trade.Duration, input.Duration);
    }

    [Fact]
    public void AnOpenTradeMapsWithNoCloseAndNoDuration()
    {
        var trade = Trade.OpenManual(new NewManualTrade
        {
            AccountId = Guid.CreateVersion7(),
            Symbol = "ETHUSDT",
            Side = TradeSide.Short,
            EntryPrice = 3000m,
            Quantity = 1m,
            OpenedAt = DateTimeOffset.UnixEpoch,
        });

        var input = AnalyticsService.ToMetricInput(trade);

        Assert.Null(input.ClosedAt);
        Assert.Null(input.Duration);
        Assert.Equal(TradeOutcome.Open, input.Outcome);
    }
}
