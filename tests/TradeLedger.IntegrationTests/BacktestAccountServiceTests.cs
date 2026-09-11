using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TradeLedger.Core.Backtesting;
using TradeLedger.Core.Domain;
using TradeLedger.Core.Domain.Backtesting;
using TradeLedger.Core.Domain.MarketData;

namespace TradeLedger.IntegrationTests;

[Collection(nameof(PostgresCollection))]
public sealed class BacktestAccountServiceTests(PostgresFixture fixture)
{
    [Fact]
    public async Task ANewAccountOpensAtItsStartingBalance()
    {
        var userId = await AddUserAsync();
        var accountId = await AddAccountAsync(userId, 10_000m);

        await using var db = fixture.CreateContext(userId);
        var service = new BacktestAccountService(db);

        var balance = await service.GetBalanceAsync(accountId);

        Assert.Equal(10_000m, balance.StartingBalance);
        Assert.Equal(10_000m, balance.CurrentBalance);
        Assert.Equal(0m, balance.NetProfitLoss);
        Assert.Equal(0, balance.SucceededRuns);
    }

    [Fact]
    public async Task SequentialRunsChainSoEachOpensWhereTheLastClosed()
    {
        var userId = await AddUserAsync();
        var accountId = await AddAccountAsync(userId, 10_000m);

        await AddRunAsync(userId, accountId, "2025-01-01", "2025-02-01", 10_000m, 11_000m);
        await AddRunAsync(userId, accountId, "2025-02-01", "2025-03-01", 11_000m, 12_500m);
        await AddRunAsync(userId, accountId, "2025-03-01", "2025-04-01", 12_500m, 11_800m);

        await using var db = fixture.CreateContext(userId);
        var service = new BacktestAccountService(db);

        var balance = await service.GetBalanceAsync(accountId);

        Assert.Equal(11_800m, balance.CurrentBalance);
        Assert.Equal(1_800m, balance.NetProfitLoss);
        Assert.Equal(3, balance.SucceededRuns);
        Assert.Equal(11_800m, await service.ResolveOpeningBalanceAsync(accountId));
    }

    [Fact]
    public async Task AnIndependentAccountNeverCompounds()
    {
        var userId = await AddUserAsync();
        var accountId = await AddAccountAsync(
            userId, 10_000m, BacktestAccountMode.Independent);

        await AddRunAsync(userId, accountId, "2025-01-01", "2025-02-01", 10_000m, 11_000m);
        await AddRunAsync(userId, accountId, "2025-01-01", "2025-02-01", 10_000m, 9_000m);

        await using var db = fixture.CreateContext(userId);
        var service = new BacktestAccountService(db);

        Assert.Equal(10_000m, (await service.GetBalanceAsync(accountId)).CurrentBalance);
        Assert.Equal(10_000m, await service.ResolveOpeningBalanceAsync(accountId));
    }

    [Fact]
    public async Task OnlySucceededRunsMoveTheBalance()
    {
        var userId = await AddUserAsync();
        var accountId = await AddAccountAsync(userId, 10_000m);

        await AddRunAsync(userId, accountId, "2025-01-01", "2025-02-01", 10_000m, 11_000m);
        await AddRunAsync(
            userId, accountId, "2025-02-01", "2025-03-01", 11_000m, 50_000m,
            BacktestStatus.Failed);
        await AddRunAsync(
            userId, accountId, "2025-03-01", "2025-04-01", 11_000m, 99_000m,
            BacktestStatus.Cancelled);

        await using var db = fixture.CreateContext(userId);

        Assert.Equal(
            11_000m,
            (await new BacktestAccountService(db).GetBalanceAsync(accountId)).CurrentBalance);
    }

    [Fact]
    public async Task AnOverlappingRangeIsRejectedOnASequentialAccount()
    {
        var userId = await AddUserAsync();
        var accountId = await AddAccountAsync(userId, 10_000m);

        await AddRunAsync(userId, accountId, "2025-01-01", "2025-03-01", 10_000m, 11_000m);

        await using var db = fixture.CreateContext(userId);
        var service = new BacktestAccountService(db);

        Assert.True(await service.OverlapsExistingRunAsync(
            accountId, At("2025-02-01"), At("2025-04-01")));

        Assert.True(await service.OverlapsExistingRunAsync(
            accountId, At("2024-12-01"), At("2025-01-15")));

        Assert.True(await service.OverlapsExistingRunAsync(
            accountId, At("2025-01-10"), At("2025-01-20")));
    }

    [Fact]
    public async Task AnAdjacentRangeDoesNotCountAsOverlapping()
    {
        var userId = await AddUserAsync();
        var accountId = await AddAccountAsync(userId, 10_000m);

        await AddRunAsync(userId, accountId, "2025-01-01", "2025-03-01", 10_000m, 11_000m);

        await using var db = fixture.CreateContext(userId);

        Assert.False(await new BacktestAccountService(db).OverlapsExistingRunAsync(
            accountId, At("2025-03-01"), At("2025-05-01")));
    }

    [Fact]
    public async Task AnIndependentAccountPermitsOverlappingRanges()
    {
        var userId = await AddUserAsync();
        var accountId = await AddAccountAsync(
            userId, 10_000m, BacktestAccountMode.Independent);

        await AddRunAsync(userId, accountId, "2025-01-01", "2025-03-01", 10_000m, 11_000m);

        await using var db = fixture.CreateContext(userId);

        Assert.False(await new BacktestAccountService(db).OverlapsExistingRunAsync(
            accountId, At("2025-02-01"), At("2025-04-01")));
    }

    [Fact]
    public async Task AQueuedRunAlreadyReservesItsRange()
    {
        var userId = await AddUserAsync();
        var accountId = await AddAccountAsync(userId, 10_000m);

        await AddRunAsync(
            userId, accountId, "2025-01-01", "2025-03-01", 10_000m, null,
            BacktestStatus.Queued);

        await using var db = fixture.CreateContext(userId);

        Assert.True(await new BacktestAccountService(db).OverlapsExistingRunAsync(
            accountId, At("2025-02-01"), At("2025-04-01")));
    }

    [Fact]
    public async Task OnlyTheMostRecentRunMayBeDeletedFromAChain()
    {
        var userId = await AddUserAsync();
        var accountId = await AddAccountAsync(userId, 10_000m);

        var first = await AddRunAsync(userId, accountId, "2025-01-01", "2025-02-01", 10_000m, 11_000m);
        var last = await AddRunAsync(userId, accountId, "2025-02-01", "2025-03-01", 11_000m, 12_000m);

        await using var db = fixture.CreateContext(userId);
        var service = new BacktestAccountService(db);

        Assert.True(await service.IsMostRecentRunAsync(accountId, last));
        Assert.False(await service.IsMostRecentRunAsync(accountId, first));
    }

    [Fact]
    public async Task TheStitchedCurveConcatenatesEveryRunInDateOrder()
    {
        var userId = await AddUserAsync();
        var accountId = await AddAccountAsync(userId, 10_000m);

        var first = await AddRunAsync(userId, accountId, "2025-01-01", "2025-02-01", 10_000m, 11_000m);
        var second = await AddRunAsync(userId, accountId, "2025-02-01", "2025-03-01", 11_000m, 9_000m);

        await AddEquityPointsAsync(userId, first, "2025-01-05", [10_500m, 11_000m]);
        await AddEquityPointsAsync(userId, second, "2025-02-05", [10_000m, 9_000m]);

        await using var db = fixture.CreateContext(userId);
        var curve = await new BacktestAccountService(db).GetStitchedEquityCurveAsync(accountId);

        Assert.Equal(4, curve.Points.Count);
        Assert.Equal(10_500m, curve.StartEquity);
        Assert.Equal(9_000m, curve.CurrentEquity);
        Assert.Equal(11_000m, curve.PeakEquity);
        Assert.Equal(2_000m, curve.MaxDrawdown);
    }

    [Fact]
    public async Task TheStitchedCurveIgnoresRunsThatDidNotSucceed()
    {
        var userId = await AddUserAsync();
        var accountId = await AddAccountAsync(userId, 10_000m);

        var failed = await AddRunAsync(
            userId, accountId, "2025-01-01", "2025-02-01", 10_000m, null, BacktestStatus.Failed);

        await AddEquityPointsAsync(userId, failed, "2025-01-05", [99_999m]);

        await using var db = fixture.CreateContext(userId);
        var curve = await new BacktestAccountService(db).GetStitchedEquityCurveAsync(accountId);

        Assert.Empty(curve.Points);
    }

    [Fact]
    public async Task TheAccountSummaryScoresEverySimulatedTrade()
    {
        var userId = await AddUserAsync();
        var accountId = await AddAccountAsync(userId, 10_000m);
        var runId = await AddRunAsync(userId, accountId, "2025-01-01", "2025-02-01", 10_000m, 10_400m);

        await AddTradesAsync(userId, runId,
        [
            (200m, TradeOutcome.Win),
            (300m, TradeOutcome.Win),
            (-100m, TradeOutcome.Loss),
        ]);

        await using var db = fixture.CreateContext(userId);
        var summary = await new BacktestAccountService(db).GetSummaryAsync(accountId);

        Assert.Equal(3, summary.TotalTrades);
        Assert.Equal(2, summary.WinningTrades);
        Assert.Equal(1, summary.LosingTrades);
        Assert.Equal(400m, summary.NetProfitLoss);
        Assert.Equal(5m, summary.ProfitFactor);
    }

    [Fact]
    public async Task BacktestDataIsInvisibleToAnotherUser()
    {
        var owner = await AddUserAsync();
        var stranger = await AddUserAsync();

        var accountId = await AddAccountAsync(owner, 10_000m);
        var runId = await AddRunAsync(owner, accountId, "2025-01-01", "2025-02-01", 10_000m, 11_000m);
        await AddTradesAsync(owner, runId, [(100m, TradeOutcome.Win)]);

        await using var asStranger = fixture.CreateContext(stranger);

        Assert.False(await asStranger.BacktestAccounts.AnyAsync(a => a.Id == accountId));
        Assert.False(await asStranger.BacktestRuns.AnyAsync(r => r.Id == runId));
        Assert.False(await asStranger.BacktestTrades.AnyAsync(t => t.BacktestRunId == runId));
    }

    [Fact]
    public async Task DeletingARunCascadesToItsTradesAndEquityPoints()
    {
        var userId = await AddUserAsync();
        var accountId = await AddAccountAsync(userId, 10_000m);
        var runId = await AddRunAsync(userId, accountId, "2025-01-01", "2025-02-01", 10_000m, 11_000m);

        await AddTradesAsync(userId, runId, [(100m, TradeOutcome.Win)]);
        await AddEquityPointsAsync(userId, runId, "2025-01-05", [10_100m]);

        await using (var db = fixture.CreateContext(userId))
        {
            db.BacktestRuns.Remove(await db.BacktestRuns.FirstAsync(r => r.Id == runId));
            await db.SaveChangesAsync();
        }

        await using var check = fixture.CreateContext(userId);

        Assert.False(await check.BacktestTrades.AnyAsync(t => t.BacktestRunId == runId));
        Assert.False(await check.BacktestEquityPoints.AnyAsync(p => p.BacktestRunId == runId));
    }

    [Fact]
    public async Task DeletingAnAccountCascadesToItsRuns()
    {
        var userId = await AddUserAsync();
        var accountId = await AddAccountAsync(userId, 10_000m);
        var runId = await AddRunAsync(userId, accountId, "2025-01-01", "2025-02-01", 10_000m, 11_000m);

        await using (var db = fixture.CreateContext(userId))
        {
            db.BacktestAccounts.Remove(await db.BacktestAccounts.FirstAsync(a => a.Id == accountId));
            await db.SaveChangesAsync();
        }

        await using var check = fixture.CreateContext(userId);

        Assert.False(await check.BacktestRuns.AnyAsync(r => r.Id == runId));
    }

    private async Task<Guid> AddAccountAsync(
        Guid userId,
        decimal startingBalance,
        BacktestAccountMode mode = BacktestAccountMode.Sequential)
    {
        await using var db = fixture.CreateContext(userId);

        var account = BacktestAccount.Create(
            $"Account {Guid.CreateVersion7():N}"[..24],
            startingBalance,
            mode,
            userId: userId);

        db.BacktestAccounts.Add(account);
        await db.SaveChangesAsync();

        return account.Id;
    }

    private async Task<Guid> AddRunAsync(
        Guid userId,
        Guid accountId,
        string from,
        string to,
        decimal opening,
        decimal? closing,
        BacktestStatus status = BacktestStatus.Succeeded)
    {
        await using var db = fixture.CreateContext(userId);

        var run = BacktestRun.Queue(new NewBacktestRun
        {
            UserId = userId,
            BacktestAccountId = accountId,
            Kind = BacktestKind.RuleEngine,
            Symbol = "BTCUSDT",
            Source = CandleSource.BinanceFutures,
            Interval = CandleInterval.OneHour,
            From = At(from),
            To = At(to),
            RiskPercentPerPosition = 2m,
            RiskRewardRatio = 2m,
            Leverage = 5,
            EngineVersion = 1,
        });

        DriveTo(run, status, opening, closing);

        db.BacktestRuns.Add(run);
        await db.SaveChangesAsync();

        return run.Id;
    }

    private static void DriveTo(
        BacktestRun run,
        BacktestStatus status,
        decimal opening,
        decimal? closing)
    {
        if (status == BacktestStatus.Queued)
        {
            return;
        }

        run.Start(opening);

        switch (status)
        {
            case BacktestStatus.Running:
                break;

            case BacktestStatus.Succeeded:
                run.Succeed(closing ?? opening, "{}", null);

                break;

            case BacktestStatus.Failed:
                run.Fail("seeded as failed");

                break;

            case BacktestStatus.Cancelled:
                run.Cancel();

                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, null);
        }
    }

    private async Task AddTradesAsync(
        Guid userId,
        Guid runId,
        IReadOnlyList<(decimal Net, TradeOutcome Outcome)> trades)
    {
        await using var db = fixture.CreateContext(userId);

        for (var i = 0; i < trades.Count; i++)
        {
            var trade = BacktestTrade.Close(new ClosedBacktestPosition
            {
                Sequence = i + 1,
                Symbol = "BTCUSDT",
                Side = TradeSide.Long,
                OpenedAt = At("2025-01-05").AddHours(i),
                ClosedAt = At("2025-01-05").AddHours(i + 2),
                EntryBarIndex = i,
                ExitBarIndex = i + 2,
                EntryPrice = 100m,
                ExitPrice = 104m,
                Quantity = 1m,
                Leverage = 5,
                PositionMargin = 20m,
                OrderValue = 100m,
                StopLossPrice = 98m,
                TakeProfitPrice = 104m,
                LiquidationPrice = 90m,
                GrossProfitLoss = trades[i].Net,
                Fees = 0m,
                Funding = 0m,
                RiskAmount = 2m,
                MaxAdverse = 0m,
                MaxFavourable = 0m,
                BalanceAfter = 0m,
                PlannedReturnR = 2m,
                ExitReason = BacktestExitReason.TakeProfit,
                IntrabarResolution = IntrabarResolution.Unambiguous,
            });

            trade.BelongsTo(userId, runId);

            Assert.Equal(trades[i].Outcome, trade.Outcome);

            db.BacktestTrades.Add(trade);
        }

        await db.SaveChangesAsync();
    }

    private async Task AddEquityPointsAsync(
        Guid userId,
        Guid runId,
        string start,
        IReadOnlyList<decimal> equities)
    {
        await using var db = fixture.CreateContext(userId);

        for (var i = 0; i < equities.Count; i++)
        {
            var point = BacktestEquityPoint.Mark(
                i + 1,
                At(start).AddDays(i),
                equities[i],
                equities.Take(i + 1).Max());

            point.BelongsTo(userId, runId);

            db.BacktestEquityPoints.Add(point);
        }

        await db.SaveChangesAsync();
    }

    private async Task<Guid> AddUserAsync()
    {
        var userId = Guid.CreateVersion7();
        var email = $"{userId:N}@example.test";

        await using var db = fixture.CreateContext(userId);

        db.Users.Add(new AppUser
        {
            Id = userId,
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            SecurityStamp = Guid.NewGuid().ToString(),
            PasswordHash = new PasswordHasher<AppUser>().HashPassword(null!, "not-used-in-tests"),
        });

        await db.SaveChangesAsync();
        return userId;
    }

    private static DateTimeOffset At(string date) =>
        DateTimeOffset.Parse(date + "T00:00:00Z", CultureInfo.InvariantCulture);
}
