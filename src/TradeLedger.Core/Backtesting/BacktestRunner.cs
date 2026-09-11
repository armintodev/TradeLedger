using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TradeLedger.Core.Analytics;
using TradeLedger.Core.Domain.Backtesting;
using TradeLedger.Core.Domain.MarketData;
using TradeLedger.Core.MarketData;
using TradeLedger.Core.Persistence;
using TradeLedger.Core.Shared.Proxy;

namespace TradeLedger.Core.Backtesting;

public sealed record BacktestResultSummary
{
    public PerformanceSummary? Performance { get; init; }

    public decimal MaxIntrabarDrawdown { get; init; }
    public decimal MaxIntrabarDrawdownPercent { get; init; }

    public int AmbiguousSignals { get; init; }
    public int SkippedInvalidStop { get; init; }
    public int SkippedInsufficientMargin { get; init; }
    public int SkippedNoCandleData { get; init; }
    public int LiquidationRiskCount { get; init; }
    public int OpenAtEndOfData { get; init; }

    public int ResolvedUnambiguous { get; init; }
    public int ResolvedByMinute { get; init; }
    public int AssumedWithinMinute { get; init; }
    public int AssumedNoMinuteData { get; init; }
}

public sealed record BacktestEngineResult(
    IReadOnlyList<BacktestTrade> Trades,
    IReadOnlyList<BacktestEquityPoint> EquityPoints,
    decimal ClosingBalance,
    BacktestResultSummary Summary,
    IReadOnlyList<string> Warnings);

public interface IBacktestProgress
{
    Task ReportAsync(int barsProcessed, int totalBars, CancellationToken ct);

    Task<bool> IsCancellationRequestedAsync(CancellationToken ct);
}

public interface IBacktestEngine
{
    BacktestKind Kind { get; }

    Task<BacktestEngineResult> RunAsync(
        BacktestRun run,
        IBacktestProgress progress,
        CancellationToken ct);
}

public sealed class BacktestCancelledException() : Exception("The run was cancelled.");

public sealed class BacktestRunner(
    TradeLedgerDbContext db,
    BacktestAccountService accounts,
    IEnumerable<IBacktestEngine> engines,
    ILogger<BacktestRunner> logger)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public bool SupportsKind(BacktestKind kind) => engines.Any(e => e.Kind == kind);

    public async Task RunAsync(Guid runId, CancellationToken ct = default)
    {
        db.BypassUserFilter = true;

        var run = await db.BacktestRuns
            .FirstOrDefaultAsync(r => r.Id == runId, ct)
            .ConfigureAwait(false);

        if (run is null || run.Status != BacktestStatus.Queued)
        {
            return;
        }

        var engine = engines.FirstOrDefault(e => e.Kind == run.Kind);

        if (engine is null)
        {
            await FailAsync(
                run,
                $"No backtest engine is registered for {run.Kind}. " +
                "This feature is not implemented yet.").ConfigureAwait(false);

            return;
        }

        run.Start(await accounts.ResolveOpeningBalanceAsync(run.BacktestAccountId, ct));

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        var progress = new DatabaseProgress(db, run);

        try
        {
            var result = await engine.RunAsync(run, progress, ct).ConfigureAwait(false);

            await PersistAsync(run, result, ct).ConfigureAwait(false);

            logger.LogInformation(
                "Backtest {RunId} finished with {Trades} trades, closing at {Balance}.",
                run.Id, result.Trades.Count, result.ClosingBalance);
        }
        catch (BacktestCancelledException)
        {
            await CancelAsync(run).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await CancelAsync(run).ConfigureAwait(false);
            throw;
        }
        catch (ProxyRequiredException ex)
        {
            await FailAsync(run, ex.Message).ConfigureAwait(false);
        }
        catch (MarketDataException ex)
        {
            await FailAsync(run, ex.Message).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            await FailAsync(run, ex.Message).ConfigureAwait(false);
        }
        catch (ArgumentException ex)
        {
            await FailAsync(run, ex.Message).ConfigureAwait(false);
        }
    }

    private async Task PersistAsync(
        BacktestRun run,
        BacktestEngineResult result,
        CancellationToken ct)
    {
        foreach (var trade in result.Trades)
        {
            trade.BelongsTo(run);
        }

        foreach (var point in result.EquityPoints)
        {
            point.BelongsTo(run);
        }

        db.BacktestTrades.AddRange(result.Trades);
        db.BacktestEquityPoints.AddRange(result.EquityPoints);

        run.Succeed(
            result.ClosingBalance,
            JsonSerializer.Serialize(result.Summary, Json),
            result.Warnings.Count > 0 ? JsonSerializer.Serialize(result.Warnings, Json) : null);

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private async Task CancelAsync(BacktestRun run)
    {
        logger.LogInformation("Backtest {RunId} was cancelled; discarding partial output.", run.Id);

        await db.BacktestTrades
            .Where(t => t.BacktestRunId == run.Id)
            .ExecuteDeleteAsync(CancellationToken.None)
            .ConfigureAwait(false);

        await db.BacktestEquityPoints
            .Where(p => p.BacktestRunId == run.Id)
            .ExecuteDeleteAsync(CancellationToken.None)
            .ConfigureAwait(false);

        run.Cancel();

        await db.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
    }

    private async Task FailAsync(BacktestRun run, string error)
    {
        logger.LogError("Backtest {RunId} failed: {Error}", run.Id, error);

        run.Fail(error);

        await db.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
    }

    private sealed class DatabaseProgress(TradeLedgerDbContext db, BacktestRun run) : IBacktestProgress
    {
        private static readonly TimeSpan MinimumInterval = TimeSpan.FromSeconds(2);

        private DateTimeOffset _lastWrite = DateTimeOffset.MinValue;

        public async Task ReportAsync(int barsProcessed, int totalBars, CancellationToken ct)
        {
            var now = DateTimeOffset.UtcNow;

            if (now - _lastWrite < MinimumInterval && barsProcessed < totalBars)
            {
                return;
            }

            _lastWrite = now;

            run.ReportProgress(barsProcessed, totalBars);

            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        public async Task<bool> IsCancellationRequestedAsync(CancellationToken ct) =>
            await db.BacktestRuns
                .AsNoTracking()
                .Where(r => r.Id == run.Id)
                .Select(r => r.CancellationRequested)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);
    }
}
