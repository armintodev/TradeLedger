using Microsoft.EntityFrameworkCore;
using TradeLedger.Core.Analytics;
using TradeLedger.Core.Domain.Backtesting;
using TradeLedger.Core.Persistence;

namespace TradeLedger.Core.Backtesting;

public sealed record AccountBalance(
    decimal StartingBalance,
    decimal CurrentBalance,
    decimal NetProfitLoss,
    int SucceededRuns);

public sealed class BacktestAccountService(TradeLedgerDbContext db)
{
    public async Task<AccountBalance> GetBalanceAsync(
        Guid accountId,
        CancellationToken ct = default)
    {
        var account = await db.BacktestAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == accountId, ct)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Backtest account {accountId} was not found.");

        var succeeded = await db.BacktestRuns
            .AsNoTracking()
            .Where(r => r.BacktestAccountId == accountId && r.Status == BacktestStatus.Succeeded)
            .Select(r => new { r.OpeningBalance, r.ClosingBalance })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var net = succeeded.Sum(r => (r.ClosingBalance ?? r.OpeningBalance) - r.OpeningBalance);

        var current = account.Mode == BacktestAccountMode.Sequential
            ? account.StartingBalance + net
            : account.StartingBalance;

        return new AccountBalance(account.StartingBalance, current, net, succeeded.Count);
    }

    public async Task<decimal> ResolveOpeningBalanceAsync(
        Guid accountId,
        CancellationToken ct = default)
    {
        var balance = await GetBalanceAsync(accountId, ct).ConfigureAwait(false);

        return balance.CurrentBalance;
    }

    public async Task<bool> OverlapsExistingRunAsync(
        Guid accountId,
        DateTimeOffset from,
        DateTimeOffset to,
        Guid? excludeRunId = null,
        CancellationToken ct = default)
    {
        var mode = await db.BacktestAccounts
            .AsNoTracking()
            .Where(a => a.Id == accountId)
            .Select(a => a.Mode)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (mode != BacktestAccountMode.Sequential)
        {
            return false;
        }

        return await db.BacktestRuns
            .AsNoTracking()
            .Where(r => r.BacktestAccountId == accountId)
            .Where(r => r.Status == BacktestStatus.Succeeded
                        || r.Status == BacktestStatus.Queued
                        || r.Status == BacktestStatus.Running)
            .Where(r => excludeRunId == null || r.Id != excludeRunId)
            .AnyAsync(r => r.From < to && from < r.To, ct)
            .ConfigureAwait(false);
    }

    public async Task<bool> IsMostRecentRunAsync(
        Guid accountId,
        Guid runId,
        CancellationToken ct = default)
    {
        var latest = await db.BacktestRuns
            .AsNoTracking()
            .Where(r => r.BacktestAccountId == accountId && r.Status == BacktestStatus.Succeeded)
            .OrderByDescending(r => r.From)
            .Select(r => r.Id)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        return latest == Guid.Empty || latest == runId;
    }

    public async Task<EquityCurve> GetStitchedEquityCurveAsync(
        Guid accountId,
        CancellationToken ct = default)
    {
        var points = await db.BacktestEquityPoints
            .AsNoTracking()
            .Where(p => db.BacktestRuns
                .Where(r => r.BacktestAccountId == accountId
                            && r.Status == BacktestStatus.Succeeded)
                .Select(r => r.Id)
                .Contains(p.BacktestRunId))
            .OrderBy(p => p.At)
            .ThenBy(p => p.Sequence)
            .Select(p => new EquityPoint(p.At, p.Equity))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return EquityMath.Analyse(points);
    }

    public async Task<PerformanceSummary> GetSummaryAsync(
        Guid accountId,
        CancellationToken ct = default)
    {
        var trades = await db.BacktestTrades
            .AsNoTracking()
            .Where(t => db.BacktestRuns
                .Where(r => r.BacktestAccountId == accountId
                            && r.Status == BacktestStatus.Succeeded)
                .Select(r => r.Id)
                .Contains(t.BacktestRunId))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return PerformanceMetrics.Compute([.. trades.Select(ToMetricInput)]);
    }

    public static TradeMetricInput ToMetricInput(BacktestTrade trade) => new(
        trade.OpenedAt,
        trade.ClosedAt,
        trade.GrossProfitLoss,
        trade.Fees,
        trade.Funding,
        trade.NetProfitLoss,
        trade.Outcome,
        trade.AchievedReturnR,
        trade.PlannedReturnR,
        IsPlanned: true,
        trade.Duration);
}
