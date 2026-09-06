using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TradeLedger.Core.Domain;
using TradeLedger.Core.Integrations.Bitunix;
using TradeLedger.Core.Persistence;

namespace TradeLedger.Worker;

public sealed class SyncWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<BitunixOptions> options,
    ILogger<SyncWorker> logger) : BackgroundService
{
    private readonly BitunixOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Sync worker started; reconciling every {Interval}.", _options.ReconcileInterval);

        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunSweepAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Sync sweep failed; will retry next interval.");
            }

            await Task.Delay(_options.ReconcileInterval, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task RunSweepAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TradeLedgerDbContext>();
        var sync = scope.ServiceProvider.GetRequiredService<BitunixSyncService>();

        db.BypassUserFilter = true;

        var accounts = await db.Accounts
            .AsNoTracking()
            .Where(a => a.IsActive
                        && a.SyncMode == SyncMode.Api
                        && a.Credential != null
                        && a.Credential.IsEnabled)
            .Select(a => a.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (accounts.Count == 0)
        {
            logger.LogDebug("No API-synced accounts to reconcile.");
            return;
        }

        foreach (var accountId in accounts)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var outcome = await sync.SyncPositionsAsync(accountId, backfill: false, ct)
                    .ConfigureAwait(false);

                if (outcome.Ran && outcome.RecordsWritten > 0)
                {
                    logger.LogInformation(
                        "Account {AccountId}: {Written} new trades from {Seen} positions.",
                        accountId, outcome.RecordsWritten, outcome.RecordsSeen);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Sync failed for account {AccountId}.", accountId);
            }
        }
    }
}
