using TradeLedger.Core.Domain;
using TradeLedger.Core.Domain.Backtesting;
using TradeLedger.Core.Domain.MarketData;

namespace TradeLedger.UnitTests.Backtesting;

public class BacktestLifecycleTests
{
    private static readonly DateTimeOffset From = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset To = new(2025, 2, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AQueuedRunStartsThenSucceeds()
    {
        var run = Queue();

        Assert.True(run.IsQueued);

        run.Start(10_000m);

        Assert.True(run.IsRunning);
        Assert.Equal(10_000m, run.OpeningBalance);
        Assert.NotNull(run.StartedAt);

        run.Succeed(11_500m, "{}", null);

        Assert.Equal(BacktestStatus.Succeeded, run.Status);
        Assert.Equal(11_500m, run.ClosingBalance);
        Assert.Equal(100m, run.ProgressPercent);
        Assert.True(run.IsFinished);
    }

    [Fact]
    public void ARunCannotStartTwice()
    {
        var run = Queue();
        run.Start(10_000m);

        var thrown = Assert.Throws<DomainRuleException>(() => run.Start(10_000m));

        Assert.Equal("run_not_queued", thrown.Code);
    }

    [Fact]
    public void AFinishedRunCannotBeCancelled()
    {
        var run = Queue();
        run.Start(10_000m);
        run.Succeed(11_000m, "{}", null);

        var thrown = Assert.Throws<ResourceConflictException>(run.RequestCancellation);

        Assert.Equal("run_already_finished", thrown.Code);
    }

    [Fact]
    public void ARunningRunAcceptsACancellationRequest()
    {
        var run = Queue();
        run.Start(10_000m);

        run.RequestCancellation();

        Assert.True(run.CancellationRequested);
    }

    [Fact]
    public void ProgressIsAPercentageOfTheBarsProcessed()
    {
        var run = Queue();

        run.ReportProgress(250, 1000);

        Assert.Equal(25m, run.ProgressPercent);
        Assert.Equal(250, run.BarsProcessed);
        Assert.Equal(1000, run.TotalBars);
    }

    [Fact]
    public void ProgressNeverExceedsOneHundredPercent()
    {
        var run = Queue();

        run.ReportProgress(1200, 1000);

        Assert.Equal(100m, run.ProgressPercent);
    }

    [Fact]
    public void ARunWindowThatEndsBeforeItStartsIsRejected()
    {
        var thrown = Assert.Throws<DomainRuleException>(
            () => Queue(from: To, to: From));

        Assert.Equal("empty_backtest_window", thrown.Code);
    }

    [Fact]
    public void ARuleEngineRunOnAOneMinuteIntervalIsRejected()
    {
        var thrown = Assert.Throws<DomainRuleException>(
            () => Queue(interval: CandleInterval.OneMinute));

        Assert.Equal("untradeable_interval", thrown.Code);
    }

    [Fact]
    public void ARuleEngineRunWithoutASymbolIsRejected()
    {
        var thrown = Assert.Throws<DomainRuleException>(() => Queue(symbol: null));

        Assert.Equal("rule_engine_needs_symbol", thrown.Code);
    }

    [Fact]
    public void OnlyQueuedRunningAndSucceededRunsTouchTheAccountBalance()
    {
        var succeeded = Queue();
        succeeded.Start(10_000m);
        succeeded.Succeed(10_500m, "{}", null);

        var failed = Queue();
        failed.Start(10_000m);
        failed.Fail("engine blew up");

        var cancelled = Queue();
        cancelled.Start(10_000m);
        cancelled.Cancel();

        Assert.True(succeeded.CountsAgainstAccountBalance);
        Assert.True(Queue().CountsAgainstAccountBalance);
        Assert.False(failed.CountsAgainstAccountBalance);
        Assert.False(cancelled.CountsAgainstAccountBalance);
    }

    [Fact]
    public void ASequentialAccountWithRunsRefusesToStayCompounding()
    {
        var account = BacktestAccount.Create("Independent", 10_000m, BacktestAccountMode.Independent);

        var thrown = Assert.Throws<ResourceConflictException>(
            () => account.SwitchMode(BacktestAccountMode.Sequential, hasRuns: true));

        Assert.Equal("cannot_switch_to_sequential", thrown.Code);
    }

    [Fact]
    public void AnEmptyAccountMaySwitchToSequential()
    {
        var account = BacktestAccount.Create("Independent", 10_000m, BacktestAccountMode.Independent);

        account.SwitchMode(BacktestAccountMode.Sequential, hasRuns: false);

        Assert.True(account.IsSequential);
    }

    [Fact]
    public void AnArchivedAccountRefusesNewRuns()
    {
        var account = BacktestAccount.Create("Archived", 10_000m);
        account.Deactivate();

        Assert.Throws<ResourceConflictException>(account.EnsureAcceptsNewRuns);
    }

    [Fact]
    public void RevisingAStrategyWithTheSameRulesDoesNotBumpTheVersion()
    {
        var strategy = BacktestStrategy.Create("EMA cross", "{}", "hash-1");

        strategy.Revise("{}", "hash-1");

        Assert.Equal(1, strategy.Version);

        strategy.Revise("{ }", "hash-2");

        Assert.Equal(2, strategy.Version);
    }

    [Fact]
    public void ABacktestTradeDerivesItsOwnOutcomeAndRMultiples()
    {
        var trade = BacktestTrade.Close(new ClosedBacktestPosition
        {
            Sequence = 1,
            Symbol = "BTCUSDT",
            Side = TradeSide.Long,
            OpenedAt = From,
            ClosedAt = From.AddHours(4),
            EntryBarIndex = 10,
            ExitBarIndex = 14,
            EntryPrice = 100m,
            ExitPrice = 104m,
            Quantity = 50m,
            Leverage = 5,
            PositionMargin = 1000m,
            OrderValue = 5000m,
            StopLossPrice = 98m,
            TakeProfitPrice = 104m,
            LiquidationPrice = 82m,
            GrossProfitLoss = 200m,
            Fees = 10m,
            Funding = -2m,
            RiskAmount = 100m,
            MaxAdverse = 40m,
            MaxFavourable = 220m,
            BalanceAfter = 10_188m,
            PlannedReturnR = 2m,
            ExitReason = BacktestExitReason.TakeProfit,
            IntrabarResolution = IntrabarResolution.Unambiguous,
        });

        Assert.Equal(188m, trade.NetProfitLoss);
        Assert.Equal(TradeOutcome.Win, trade.Outcome);
        Assert.Equal(1.88m, trade.AchievedReturnR);
        Assert.Equal(0.4m, trade.MaeR);
        Assert.Equal(2.2m, trade.MfeR);
        Assert.Equal(4, trade.BarsInTrade);
        Assert.Equal(TimeSpan.FromHours(4), trade.Duration);
        Assert.False(trade.WasLiquidated);
        Assert.False(trade.ExitWasAssumed);
    }

    [Fact]
    public void AnAssumedExitIsFlaggedSoTheResultCanBeDistrusted()
    {
        var trade = BacktestTrade.Close(new ClosedBacktestPosition
        {
            Sequence = 1,
            Symbol = "BTCUSDT",
            Side = TradeSide.Long,
            OpenedAt = From,
            ClosedAt = From.AddHours(1),
            EntryBarIndex = 0,
            ExitBarIndex = 1,
            EntryPrice = 100m,
            ExitPrice = 98m,
            Quantity = 1m,
            Leverage = 1,
            PositionMargin = 100m,
            OrderValue = 100m,
            StopLossPrice = 98m,
            TakeProfitPrice = 104m,
            LiquidationPrice = 0m,
            GrossProfitLoss = -2m,
            Fees = 0m,
            Funding = 0m,
            RiskAmount = 2m,
            MaxAdverse = 2m,
            MaxFavourable = 0m,
            BalanceAfter = 9998m,
            PlannedReturnR = 2m,
            ExitReason = BacktestExitReason.StopLoss,
            IntrabarResolution = IntrabarResolution.AssumedNoMinuteData,
        });

        Assert.True(trade.ExitWasAssumed);
        Assert.Equal(-1m, trade.AchievedReturnR);
    }

    private static BacktestRun Queue(
        string? symbol = "BTCUSDT",
        CandleInterval? interval = CandleInterval.OneHour,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null) =>
        BacktestRun.Queue(new NewBacktestRun
        {
            BacktestAccountId = Guid.CreateVersion7(),
            Kind = BacktestKind.RuleEngine,
            Symbol = symbol,
            Source = CandleSource.BinanceFutures,
            Interval = interval,
            From = from ?? From,
            To = to ?? To,
            RiskPercentPerPosition = 2m,
            RiskRewardRatio = 2m,
            Leverage = 5,
            EngineVersion = 1,
        });
}
