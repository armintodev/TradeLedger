using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TradeLedger.Core.Domain.MarketData;
using TradeLedger.Core.MarketData;
using TradeLedger.Core.Persistence;
using TradeLedger.Core.Shared;

namespace TradeLedger.Worker;

public sealed class MarketDataWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<MarketDataOptions> options,
    ILogger<MarketDataWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan LockTtl = TimeSpan.FromMinutes(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Market data worker started; polling every {Interval}, {Rps} requests/second budget.",
            PollInterval, options.Value.RequestsPerSecond);

        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var ran = await DrainOnceAsync(stoppingToken).ConfigureAwait(false);

                if (ran)
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
                logger.LogError(ex, "Market data sweep failed; will retry next interval.");
            }

            await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task<bool> DrainOnceAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<TradeLedgerDbContext>();
        var locks = scope.ServiceProvider.GetRequiredService<IDistributedLock>();
        var backfill = scope.ServiceProvider.GetRequiredService<MarketDataBackfillService>();

        db.BypassUserFilter = true;

        var jobId = await db.MarketDataBackfillJobs
            .AsNoTracking()
            .Where(j => j.Status == MarketDataJobStatus.Queued && !j.CancellationRequested)
            .OrderBy(j => j.QueuedAt)
            .Select(j => j.Id)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (jobId == Guid.Empty)
        {
            return false;
        }

        await using var handle = await locks
            .TryAcquireAsync($"tl:lock:marketdata:{jobId}", LockTtl, ct)
            .ConfigureAwait(false);

        if (handle is null)
        {
            return false;
        }

        await backfill.RunAsync(jobId, ct).ConfigureAwait(false);

        return true;
    }
}
