using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradeLedger.Core.Domain;
using TradeLedger.Core.Integrations.Bitunix.Dtos;
using TradeLedger.Core.Persistence;
using TradeLedger.Core.Shared;
using TradeLedger.Core.Shared.Proxy;

namespace TradeLedger.Core.Integrations.Bitunix;

public sealed class BitunixSyncService(
    TradeLedgerDbContext db,
    IUnitOfWork unitOfWork,
    BitunixClient client,
    ICredentialProtector protector,
    IUserProxyResolver proxyResolver,
    IDistributedLock distributedLock,
    IOptions<BitunixOptions> options,
    ILogger<BitunixSyncService> logger
)
{
    public const string PositionsEndpoint = "futures.history_positions";
    public const string AccountEndpoint = "futures.account";

    private readonly BitunixOptions _options = options.Value;

    public async Task<SyncOutcome> SyncPositionsAsync(
        Guid accountId,
        bool backfill = false,
        CancellationToken ct = default)
    {
        await using var handle = await distributedLock
            .TryAcquireAsync($"sync:account:{accountId}", TimeSpan.FromMinutes(10), ct);

        if (handle is null)
        {
            logger.LogDebug("Sync for account {AccountId} skipped: another run holds the lock.", accountId);

            return SyncOutcome.Skipped;
        }

        var account = await LoadAccountAsync(accountId, ct);

        if (account is null)
        {
            return SyncOutcome.Skipped;
        }

        var connection = await ConnectAsync(account, ct);
        var cursor = await GetOrCreateCursorAsync(account, PositionsEndpoint, ct);

        var run = StartRun(account, PositionsEndpoint, backfill);

        var seen = 0;
        var written = 0;
        var requests = 0;

        try
        {
            var floorMs = cursor.WatermarkFor(backfill, account.TrackedFrom);

            var skip = 0;
            long? newestSeenMs = null;

            while (!ct.IsCancellationRequested)
            {
                var page = await client.GetHistoryPositionsAsync(
                    connection,
                    startTimeMs: floorMs,
                    skip: skip,
                    limit: _options.MaxPageSize,
                    ct: ct
                );

                requests++;

                var items = page?.PositionList ?? [];

                if (items.Count == 0)
                {
                    break;
                }

                await unitOfWork.ExecuteInTransactionAsync(
                    async token =>
                    {
                        foreach (var dto in items)
                        {
                            seen++;

                            if (await UpsertPositionAsync(account, dto, token))
                            {
                                written++;
                            }

                            if (dto.Ctime is { } c && (newestSeenMs is null || c > newestSeenMs))
                            {
                                newestSeenMs = c;
                            }
                        }
                    },
                    ct);

                if (items.Count < _options.MaxPageSize)
                {
                    break;
                }

                skip += items.Count;
            }

            cursor.Advance(newestSeenMs);

            if (backfill)
            {
                cursor.CompleteBackfill();
            }

            run.Succeed();

            return SyncOutcome.Completed(seen, written);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            run.Fail(ex.Message);
            logger.LogError(ex, "Bitunix position sync failed for account {AccountId}.", accountId);

            throw;
        }
        finally
        {
            run.Finish(seen, written, requests);
            await db.SaveChangesAsync(CancellationToken.None);
        }
    }

    public async Task<BalanceSnapshot?> CaptureBalanceSnapshotAsync(
        Guid accountId,
        CancellationToken ct = default)
    {
        var account = await LoadAccountAsync(accountId, ct);

        if (account is null)
        {
            return null;
        }

        var connection = await ConnectAsync(account, ct);

        var dto = await client.GetFuturesAccountAsync(connection, account.QuoteAsset, ct);

        if (dto is null)
        {
            return null;
        }

        var snapshot = BalanceSnapshot.Capture(
            BitunixPositionMapper.ToSnapshot(dto, account.Id, account.UserId));

        db.BalanceSnapshots.Add(snapshot);
        await db.SaveChangesAsync(ct);

        return snapshot;
    }

    private async Task<bool> UpsertPositionAsync(
        Account account,
        HistoryPositionDto dto,
        CancellationToken ct)
    {
        var existing = await db.Trades
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                t => t.AccountId == account.Id && t.ExchangePositionId == dto.PositionId,
                ct
            );

        var snapshot = BitunixPositionMapper.ToSnapshot(dto);

        var isNew = existing is null;
        Trade trade;

        if (existing is null)
        {
            trade = Trade.FromExchange(account.UserId, account.Id, snapshot);
            db.Trades.Add(trade);
        }
        else
        {
            trade = existing;
            trade.ApplyExchangeSnapshot(snapshot);
        }

        var balanceBefore = await GetBalanceBeforeAsync(account.Id, trade.OpenedAt, ct);

        trade.ApplyBalanceContext(balanceBefore);

        if (isNew)
        {
            await TryLinkPlanAsync(trade, ct);
        }

        StoreRawPayload(account, PositionsEndpoint, dto.PositionId, dto);

        return isNew;
    }

    private async Task TryLinkPlanAsync(Trade trade, CancellationToken ct)
    {
        var candidates = await db.TradePlans
            .IgnoreQueryFilters()
            .Where(p => p.UserId == trade.UserId
                        && p.Symbol == trade.Symbol
                        && p.Side == trade.Side
                        && (p.Status == PlanStatus.Draft || p.Status == PlanStatus.Active)
                        && p.CreatedAt <= trade.OpenedAt
                        && (p.ExpiresAt == null || p.ExpiresAt >= trade.OpenedAt)
            )
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);

        TradePlanMatcher.Link(trade, candidates);
    }

    private Task<decimal?> GetBalanceBeforeAsync(Guid accountId, DateTimeOffset openedAt, CancellationToken ct) =>
        db.BalanceSnapshots
            .IgnoreQueryFilters()
            .Where(s => s.AccountId == accountId && s.CapturedAt <= openedAt)
            .OrderByDescending(s => s.CapturedAt)
            .Select(s => (decimal?)s.Equity)
            .FirstOrDefaultAsync(ct);

    private void StoreRawPayload(Account account, string endpoint, string externalId, object dto)
    {
        var json = JsonSerializer.Serialize(dto, BitunixJson.Options);

        var existing = db.ChangeTracker.Entries<RawExchangePayload>()
            .FirstOrDefault(e => e.Entity.AccountId == account.Id
                                 && e.Entity.Endpoint == endpoint
                                 && e.Entity.ExternalId == externalId
            );

        if (existing is not null)
        {
            existing.Entity.Replace(json);

            return;
        }

        db.RawExchangePayloads.Add(
            RawExchangePayload.Capture(account.UserId, account.Id, endpoint, externalId, json));
    }

    private async Task<Account?> LoadAccountAsync(Guid accountId, CancellationToken ct)
    {
        var account = await db.Accounts
            .IgnoreQueryFilters()
            .Include(a => a.Credential)
            .FirstOrDefaultAsync(a => a.Id == accountId, ct);

        if (account is null)
        {
            logger.LogWarning("Sync requested for unknown account {AccountId}.", accountId);

            return null;
        }

        if (account.Credential is null || !account.Credential.IsEnabled)
        {
            logger.LogWarning("Account {AccountId} has no enabled credential; skipping.", accountId);

            return null;
        }

        return account.SyncsAutomatically ? account : null;
    }

    private BitunixCredentials Decrypt(ExchangeCredential credential) => new(
        protector.Unprotect(credential.ApiKeyCipher),
        protector.Unprotect(credential.ApiSecretCipher),
        credential.ApiKeyHint
    );

    private async Task<BitunixConnection> ConnectAsync(Account account, CancellationToken ct)
    {
        var proxy = await proxyResolver.ResolveAsync(account.UserId, ct);

        logger.LogDebug(
            "Account {AccountId} will reach Bitunix via {Egress}",
            account.Id,
            proxy?.Describe() ?? "direct");

        return new BitunixConnection(Decrypt(account.Credential!), proxy);
    }

    private async Task<SyncCursor> GetOrCreateCursorAsync(
        Account account,
        string endpoint,
        CancellationToken ct)
    {
        var cursor = await db.SyncCursors
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.AccountId == account.Id && c.Endpoint == endpoint, ct);

        if (cursor is not null)
        {
            return cursor;
        }

        cursor = SyncCursor.For(account, endpoint);

        db.SyncCursors.Add(cursor);

        return cursor;
    }

    private SyncRun StartRun(Account account, string endpoint, bool backfill)
    {
        var run = SyncRun.Start(account, endpoint, backfill);

        db.SyncRuns.Add(run);

        return run;
    }
}

public readonly record struct SyncOutcome(bool Ran, int RecordsSeen, int RecordsWritten)
{
    public static SyncOutcome Skipped => new(false, 0, 0);

    public static SyncOutcome Completed(int seen, int written) => new(true, seen, written);
}
