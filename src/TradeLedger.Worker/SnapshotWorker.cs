using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TradeLedger.Core.Domain;
using TradeLedger.Core.Integrations.Bitunix;
using TradeLedger.Core.Persistence;

namespace TradeLedger.Worker;

public sealed class SnapshotWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<BitunixOptions> options,
    ILogger<SnapshotWorker> logger) : BackgroundService
{
    private readonly BitunixOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Snapshot worker started; capturing every {Interval}.", _options.SnapshotInterval);

        await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CaptureAllAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Snapshot pass failed; will retry next interval.");
            }

            await Task.Delay(_options.SnapshotInterval, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task CaptureAllAsync(CancellationToken ct)
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

        foreach (var accountId in accounts)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                await sync.CaptureBalanceSnapshotAsync(accountId, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Snapshot failed for account {AccountId}.", accountId);
            }
        }
    }
}
