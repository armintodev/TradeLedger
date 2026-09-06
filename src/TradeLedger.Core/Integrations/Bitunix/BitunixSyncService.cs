using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradeLedger.Core.Domain;
using TradeLedger.Core.Integrations.Bitunix.Dtos;
using TradeLedger.Core.Persistence;
using TradeLedger.Core.Shared;

namespace TradeLedger.Core.Integrations.Bitunix;

public sealed class BitunixSyncService(
    TradeLedgerDbContext db,
    BitunixClient client,
    ICredentialProtector protector,
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
            .TryAcquireAsync($"sync:account:{accountId}", TimeSpan.FromMinutes(10), ct)
            .ConfigureAwait(false);

        if (handle is null)
        {
            logger.LogDebug("Sync for account {AccountId} skipped: another run holds the lock.", accountId);

            return SyncOutcome.Skipped;
        }

        var account = await LoadAccountAsync(accountId, ct).ConfigureAwait(false);

        if (account is null)
        {
            return SyncOutcome.Skipped;
        }

        var credentials = Decrypt(account.Credential!);
        var cursor = await GetOrCreateCursorAsync(account, PositionsEndpoint, ct).ConfigureAwait(false);

        var run = StartRun(account, PositionsEndpoint, backfill);

        var seen = 0;
        var written = 0;
        var requests = 0;

        try
        {
            var floorMs = backfill
                ? account.TrackedFrom?.ToUnixTimeMilliseconds()
                : cursor.LastRecordMs;

            var skip = 0;
            var newestSeenMs = cursor.LastRecordMs;

            while (!ct.IsCancellationRequested)
            {
                var page = await client.GetHistoryPositionsAsync(
                    credentials,
                    startTimeMs: floorMs,
                    skip: skip,
                    limit: _options.MaxPageSize,
                    ct: ct
                ).ConfigureAwait(false);

                requests++;

                var items = page?.PositionList ?? [];

                if (items.Count == 0)
                {
                    break;
                }

                foreach (var dto in items)
                {
                    seen++;

                    if (await UpsertPositionAsync(account, dto, ct).ConfigureAwait(false))
                    {
                        written++;
                    }

                    if (dto.Ctime is { } c && (newestSeenMs is null || c > newestSeenMs))
                    {
                        newestSeenMs = c;
                    }
                }

                await db.SaveChangesAsync(ct).ConfigureAwait(false);

                if (items.Count < _options.MaxPageSize)
                {
                    break;
                }

                skip += items.Count;
            }

            cursor.LastRecordMs = newestSeenMs;
            cursor.LastSyncedAt = DateTimeOffset.UtcNow;

            if (backfill)
            {
                cursor.BackfillComplete = true;
            }

            run.Status = SyncRunStatus.Succeeded;

            return SyncOutcome.Completed(seen, written);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            run.Status = SyncRunStatus.Failed;
            run.Error = ex.Message;
            logger.LogError(ex, "Bitunix position sync failed for account {AccountId}.", accountId);

            throw;
        }
        finally
        {
            run.FinishedAt = DateTimeOffset.UtcNow;
            run.RecordsSeen = seen;
            run.RecordsWritten = written;
            run.RequestsMade = requests;
            await db.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }

    public async Task<BalanceSnapshot?> CaptureBalanceSnapshotAsync(
        Guid accountId,
        CancellationToken ct = default)
    {
        var account = await LoadAccountAsync(accountId, ct).ConfigureAwait(false);

        if (account is null)
        {
            return null;
        }

        var credentials = Decrypt(account.Credential!);

        var dto = await client.GetFuturesAccountAsync(credentials, account.QuoteAsset, ct)
            .ConfigureAwait(false);

        if (dto is null)
        {
            return null;
        }

        var snapshot = BitunixPositionMapper.ToSnapshot(dto, account.Id, account.UserId);
        db.BalanceSnapshots.Add(snapshot);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

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
            )
            .ConfigureAwait(false);

        var isNew = existing is null;

        var trade = existing ?? new Trade
        {
            UserId = account.UserId,
            AccountId = account.Id,
            Symbol = dto.Symbol,
            ReviewState = ReviewState.Unreviewed,
        };

        BitunixPositionMapper.ApplyTo(trade, dto, account.Id);

        var balanceBefore = await GetBalanceBeforeAsync(account.Id, trade.OpenedAt, ct)
            .ConfigureAwait(false);

        trade.Recalculate(balanceBefore);

        if (isNew)
        {
            db.Trades.Add(trade);
            await TryLinkPlanAsync(trade, ct).ConfigureAwait(false);
        }

        StoreRawPayload(account, PositionsEndpoint, dto.PositionId, dto);

        return isNew;
    }

    private async Task TryLinkPlanAsync(Trade trade, CancellationToken ct)
    {
        var plan = await db.TradePlans
            .IgnoreQueryFilters()
            .Where(p => p.UserId == trade.UserId
                        && p.Symbol == trade.Symbol
                        && p.Side == trade.Side
                        && (p.Status == PlanStatus.Draft || p.Status == PlanStatus.Active)
                        && p.CreatedAt <= trade.OpenedAt
                        && (p.ExpiresAt == null || p.ExpiresAt >= trade.OpenedAt)
            )
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (plan is null)
        {
            trade.IsPlanned = false;

            return;
        }

        plan.Status = PlanStatus.Linked;
        plan.LinkedTradeId = trade.Id;

        trade.TradePlanId = plan.Id;
        trade.IsPlanned = true;

        trade.StrategyId ??= plan.StrategyId;
        trade.TimeframeId ??= plan.TimeframeId;
        trade.EntryMentalStateId ??= plan.EntryMentalStateId;
        trade.StopLossPrice ??= plan.PlannedStopLossPrice;
        trade.TakeProfitPrice ??= plan.PlannedTakeProfitPrice;
        trade.PlannedReturnR ??= plan.PlannedRiskReward;
        trade.MarketContext ??= plan.MarketContext;
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
            existing.Entity.Payload = json;

            return;
        }

        db.RawExchangePayloads.Add(
            new RawExchangePayload
            {
                UserId = account.UserId,
                AccountId = account.Id,
                Endpoint = endpoint,
                ExternalId = externalId,
                Payload = json,
            }
        );
    }

    private async Task<Account?> LoadAccountAsync(Guid accountId, CancellationToken ct)
    {
        var account = await db.Accounts
            .IgnoreQueryFilters()
            .Include(a => a.Credential)
            .FirstOrDefaultAsync(a => a.Id == accountId, ct)
            .ConfigureAwait(false);

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

        if (!account.IsActive || account.SyncMode != SyncMode.Api)
        {
            return null;
        }

        return account;
    }

    private BitunixCredentials Decrypt(ExchangeCredential credential) => new(
        protector.Unprotect(credential.ApiKeyCipher),
        protector.Unprotect(credential.ApiSecretCipher),
        credential.ApiKeyHint
    );

    private async Task<SyncCursor> GetOrCreateCursorAsync(
        Account account,
        string endpoint,
        CancellationToken ct)
    {
        var cursor = await db.SyncCursors
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.AccountId == account.Id && c.Endpoint == endpoint, ct)
            .ConfigureAwait(false);

        if (cursor is not null)
        {
            return cursor;
        }

        cursor = new SyncCursor
        {
            UserId = account.UserId,
            AccountId = account.Id,
            Endpoint = endpoint,
        };

        db.SyncCursors.Add(cursor);

        return cursor;
    }

    private SyncRun StartRun(Account account, string endpoint, bool backfill)
    {
        var run = new SyncRun
        {
            UserId = account.UserId,
            AccountId = account.Id,
            Endpoint = endpoint,
            IsBackfill = backfill,
        };

        db.SyncRuns.Add(run);

        return run;
    }
}

public readonly record struct SyncOutcome(bool Ran, int RecordsSeen, int RecordsWritten)
{
    public static SyncOutcome Skipped => new(false, 0, 0);

    public static SyncOutcome Completed(int seen, int written) => new(true, seen, written);
}
