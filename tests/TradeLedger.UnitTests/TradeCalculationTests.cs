using TradeLedger.Core.Domain;

namespace TradeLedger.UnitTests;

public class TradeCalculationTests
{
    private static readonly DateTimeOffset Opened = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void NetIsGrossMinusFeesPlusFunding()
    {
        var trade = Closed(exitPrice: 200m, fees: 3m, funding: -2m);

        Assert.Equal(100m, trade.GrossProfitLoss);
        Assert.Equal(95m, trade.NetProfitLoss);
    }

    [Fact]
    public void PositiveFundingIsCredited()
    {
        var trade = Closed(exitPrice: 200m, fees: 3m, funding: 5m);

        Assert.Equal(102m, trade.NetProfitLoss);
    }

    [Fact]
    public void OutcomeFollowsNetNotGross()
    {
        var trade = Closed(exitPrice: 105m, fees: 6m, funding: -1m);

        Assert.Equal(5m, trade.GrossProfitLoss);
        Assert.Equal(-2m, trade.NetProfitLoss);
        Assert.Equal(TradeOutcome.Loss, trade.Outcome);
    }

    [Fact]
    public void AnOpenTradeHasNoOutcomeOrDuration()
    {
        var trade = Trade.OpenManual(Spec() with { ClosedAt = null, ExitPrice = null });

        Assert.Equal(TradeOutcome.Open, trade.Outcome);
        Assert.Null(trade.Duration);
        Assert.False(trade.IsClosed);
    }

    [Fact]
    public void ZeroNetIsBreakeven()
    {
        var trade = Closed(exitPrice: 103m, fees: 3m, funding: 0m);

        Assert.Equal(TradeOutcome.Breakeven, trade.Outcome);
    }

    [Fact]
    public void AchievedRIsNetOverAmountRisked()
    {
        var trade = Trade.OpenManual(Spec() with
        {
            EntryPrice = 100m,
            StopLossPrice = 90m,
            Quantity = 2m,
            ExitPrice = 130m,
            Fees = 0m,
            Funding = 0m,
        });

        Assert.Equal(60m, trade.GrossProfitLoss);
        Assert.Equal(3m, trade.AchievedReturnR);
    }

    [Fact]
    public void AchievedRIsNullWithoutAStop()
    {
        var trade = Closed(exitPrice: 160m);

        Assert.Null(trade.StopLossPrice);
        Assert.Null(trade.AchievedReturnR);
    }

    [Fact]
    public void DurationIsExitMinusEntry()
    {
        var trade = Trade.OpenManual(Spec() with
        {
            OpenedAt = new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero),
            ClosedAt = new DateTimeOffset(2026, 1, 1, 13, 30, 0, TimeSpan.Zero),
        });

        Assert.Equal(TimeSpan.FromHours(3.5), trade.Duration);
    }

    [Fact]
    public void AccountPercentagesUseBalanceBefore()
    {
        var trade = Closed(exitPrice: 150m, fees: 0m, funding: 0m);

        trade.ApplyBalanceContext(1000m);

        Assert.Equal(5m, trade.AccountChangePercent);
        Assert.Equal(1050m, trade.BalanceAfter);
    }

    [Fact]
    public void PlannedStopPercentIsDistanceOverEntry()
    {
        var trade = Trade.OpenManual(Spec() with
        {
            EntryPrice = 200m,
            StopLossPrice = 190m,
            ExitPrice = 210m,
        });

        Assert.Equal(5m, trade.PlannedStopLossPercent);
    }

    [Fact]
    public void AShortSideStopSitsAboveEntry()
    {
        var trade = Trade.OpenManual(Spec() with
        {
            Side = TradeSide.Short,
            EntryPrice = 100m,
            StopLossPrice = 110m,
            Quantity = 1m,
            ExitPrice = 80m,
            Fees = 0m,
            Funding = 0m,
        });

        Assert.Equal(20m, trade.GrossProfitLoss);
        Assert.Equal(2m, trade.AchievedReturnR);
        Assert.Equal(10m, trade.PlannedStopLossPercent);
    }

    [Fact]
    public void AShortSellsHighAndBuysBack()
    {
        var trade = Trade.OpenManual(Spec() with
        {
            Side = TradeSide.Short,
            EntryPrice = 100m,
            ExitPrice = 120m,
            Quantity = 1m,
            Fees = 0m,
            Funding = 0m,
        });

        Assert.Equal(-20m, trade.GrossProfitLoss);
        Assert.Equal(TradeOutcome.Loss, trade.Outcome);
    }

    [Fact]
    public void ALongStopAboveEntryIsRejected()
    {
        var thrown = Assert.Throws<DomainRuleException>(
            () => Trade.OpenManual(Spec() with { EntryPrice = 100m, StopLossPrice = 110m }));

        Assert.Equal("stop_on_wrong_side", thrown.Code);
    }

    [Fact]
    public void AShortStopBelowEntryIsRejected()
    {
        var thrown = Assert.Throws<DomainRuleException>(
            () => Trade.OpenManual(Spec() with
            {
                Side = TradeSide.Short,
                EntryPrice = 100m,
                StopLossPrice = 90m,
            }));

        Assert.Equal("stop_on_wrong_side", thrown.Code);
    }

    [Fact]
    public void ALongTakeProfitBelowEntryIsRejected()
    {
        var thrown = Assert.Throws<DomainRuleException>(
            () => Trade.OpenManual(Spec() with { EntryPrice = 100m, TakeProfitPrice = 90m }));

        Assert.Equal("target_on_wrong_side", thrown.Code);
    }

    [Fact]
    public void ATradeThatClosesBeforeItOpensIsRejected()
    {
        var thrown = Assert.Throws<DomainRuleException>(
            () => Trade.OpenManual(Spec() with { ClosedAt = Opened.AddHours(-1) }));

        Assert.Equal("closed_before_opened", thrown.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void ANonPositiveEntryPriceIsRejected(int entryPrice)
    {
        var thrown = Assert.Throws<DomainValidationException>(
            () => Trade.OpenManual(Spec() with { EntryPrice = entryPrice }));

        Assert.True(thrown.Errors.ContainsKey("EntryPrice"));
    }

    [Fact]
    public void AZeroQuantityIsRejected()
    {
        Assert.Throws<DomainValidationException>(
            () => Trade.OpenManual(Spec() with { Quantity = 0m }));
    }

    [Fact]
    public void ARatingOutsideOneToFiveIsRejected()
    {
        var trade = Closed(exitPrice: 150m);

        Assert.Throws<DomainValidationException>(
            () => trade.Journal(new TradeJournalEdit { Rating = 6 }));
    }

    [Fact]
    public void AnOpenTradeCannotBeMarkedReviewed()
    {
        var trade = Trade.OpenManual(Spec() with { ClosedAt = null, ExitPrice = null });

        var thrown = Assert.Throws<DomainRuleException>(trade.MarkReviewed);

        Assert.Equal("review_open_trade", thrown.Code);
    }

    [Fact]
    public void ASyncedTradeRefusesDeletion()
    {
        var trade = Trade.FromExchange(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            new ExchangePositionSnapshot
            {
                ExchangePositionId = "pos-1",
                Symbol = "BTCUSDT",
                Side = TradeSide.Long,
                EntryPrice = 100m,
                Quantity = 1m,
                OpenedAt = Opened,
            });

        Assert.Throws<ResourceConflictException>(trade.EnsureDeletable);
    }

    [Fact]
    public void AManualTradeAllowsDeletion()
    {
        var trade = Closed(exitPrice: 150m);

        trade.EnsureDeletable();
    }

    [Fact]
    public void JournallingOnlyChangesTheFieldsYouSend()
    {
        var trade = Closed(exitPrice: 150m);
        var strategyId = Guid.CreateVersion7();

        trade.Journal(new TradeJournalEdit { StrategyId = strategyId, Memo = "First pass." });
        trade.Journal(new TradeJournalEdit { Rating = 4 });

        Assert.Equal(strategyId, trade.StrategyId);
        Assert.Equal("First pass.", trade.Memo);
        Assert.Equal(4, trade.Rating);
    }

    [Fact]
    public void ReplacingMistakesDropsThePreviousSet()
    {
        var trade = Closed(exitPrice: 150m);
        var first = Guid.CreateVersion7();
        var second = Guid.CreateVersion7();

        trade.ReplaceMistakes([first, first, second]);

        Assert.Equal(2, trade.Mistakes.Count);

        trade.ReplaceMistakes([second]);

        Assert.Single(trade.Mistakes);
        Assert.Equal(second, trade.Mistakes.Single().TaxonomyTermId);
    }

    private static Trade Closed(decimal exitPrice, decimal fees = 0m, decimal funding = 0m) =>
        Trade.OpenManual(Spec() with { ExitPrice = exitPrice, Fees = fees, Funding = funding });

    private static NewManualTrade Spec() => new()
    {
        AccountId = Guid.CreateVersion7(),
        Symbol = "BTCUSDT",
        Side = TradeSide.Long,
        EntryPrice = 100m,
        Quantity = 1m,
        OpenedAt = Opened,
        ClosedAt = Opened.AddHours(1),
    };
}
