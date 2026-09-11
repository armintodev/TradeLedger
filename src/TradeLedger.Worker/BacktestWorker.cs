using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TradeLedger.Core.Backtesting;
using TradeLedger.Core.Domain.Backtesting;
using TradeLedger.Core.Persistence;
using TradeLedger.Core.Shared;

namespace TradeLedger.Worker;

public sealed class BacktestWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<BacktestOptions> options,
    ILogger<BacktestWorker> logger) : BackgroundService
{
    private static readonly TimeSpan LockTtl = TimeSpan.FromHours(6);

    private readonly BacktestOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Backtest worker started; polling every {Interval}, engine version {Version}.",
            _options.QueuePollInterval, _options.EngineVersion);

        await Task.Delay(TimeSpan.FromSeconds(8), stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ReclaimStaleRunsAsync(stoppingToken).ConfigureAwait(false);

                if (await DrainOnceAsync(stoppingToken).ConfigureAwait(false))
                {
                    continue;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Backtest sweep failed; will retry next interval.");
            }

            await Task.Delay(_options.QueuePollInterval, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task<bool> DrainOnceAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<TradeLedgerDbContext>();
        var locks = scope.ServiceProvider.GetRequiredService<IDistributedLock>();
        var runner = scope.ServiceProvider.GetRequiredService<BacktestRunner>();

        db.BypassUserFilter = true;

        var next = await db.BacktestRuns
            .AsNoTracking()
            .Where(r => r.Status == BacktestStatus.Queued && !r.CancellationRequested)
            .OrderBy(r => r.QueuedAt)
            .Select(r => new { r.Id, r.BacktestAccountId })
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (next is null)
        {
            return false;
        }

        await using var handle = await locks
            .TryAcquireAsync($"tl:lock:backtest:account:{next.BacktestAccountId}", LockTtl, ct)
            .ConfigureAwait(false);

        if (handle is null)
        {
            return false;
        }

        await runner.RunAsync(next.Id, ct).ConfigureAwait(false);

        return true;
    }

    private async Task ReclaimStaleRunsAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<TradeLedgerDbContext>();
        db.BypassUserFilter = true;

        var cutoff = DateTimeOffset.UtcNow - _options.StaleRunTimeout;

        var stale = await db.BacktestRuns
            .Where(r => r.Status == BacktestStatus.Running
                        && r.StartedAt != null
                        && r.StartedAt < cutoff)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (stale.Count == 0)
        {
            return;
        }

        foreach (var run in stale)
        {
            logger.LogWarning(
                "Backtest {RunId} has been running since {StartedAt}; marking it lost.",
                run.Id, run.StartedAt);

            run.Fail(
                "The worker running this backtest stopped without finishing it. " +
                "Nothing was written; queue the run again.");
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
