using Microsoft.EntityFrameworkCore;
using TradeLedger.Core.Integrations.Bitunix;
using TradeLedger.Core.Persistence;
using TradeLedger.Core.Domain;
using TradeLedger.Api.Features.Portfolio;

namespace TradeLedger.Api.Features.Sync;

public static class SyncEndpoints
{
    public static void MapSyncEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/sync").WithTags("Sync").RequireAuthorization();

        group.MapGet("/status", async (TradeLedgerDbContext db, CancellationToken ct) =>
        {
            var cursors = await db.SyncCursors.AsNoTracking()
                .Select(c => new SyncStatusResponse(
                    c.AccountId,
                    c.Endpoint,
                    c.LastSyncedAt,
                    c.LastRecordMs == null
                        ? null
                        : DateTimeOffset.FromUnixTimeMilliseconds(c.LastRecordMs.Value),
                    c.BackfillComplete))
                .ToListAsync(ct);

            return Results.Ok(cursors);
        })
        .WithName("GetSyncStatus")
        .WithSummary("Sync watermarks")
        .WithDescription("Per account and endpoint: when it last synced, the timestamp of the newest record persisted, and whether the historical backfill has finished.")
        .Produces<List<SyncStatusResponse>>();

        group.MapGet("/runs", async (TradeLedgerDbContext db, CancellationToken ct) =>
        {
            var runs = await db.SyncRuns.AsNoTracking()
                .OrderByDescending(r => r.StartedAt)
                .Take(50)
                .ToListAsync(ct);

            return Results.Ok(runs.Select(SyncRunResponse.From).ToList());
        })
        .WithName("GetRecentSyncRuns")
        .WithSummary("Recent sync runs")
        .WithDescription("The last fifty sync attempts with records seen, records written, requests made and any error, so a silently stalled sync is visible.")
        .Produces<List<SyncRunResponse>>();

        group.MapPost("/accounts/{accountId:guid}/run", async (
            Guid accountId,
            BitunixSyncService sync,
            CancellationToken ct) =>
        {
            var outcome = await sync.SyncPositionsAsync(accountId, backfill: false, ct);
            return Results.Ok(outcome);
        })
        .WithName("TriggerSync")
        .WithSummary("Run a sync now")
        .WithDescription("Forces an incremental reconcile sweep for one account rather than waiting for the scheduled run. Writes are idempotent, so running this repeatedly is safe and is a no-op when nothing is new.")
        .Produces<SyncOutcome>();

        group.MapPost("/accounts/{accountId:guid}/backfill", async (
            Guid accountId,
            BitunixSyncService sync,
            CancellationToken ct) =>
        {
            var outcome = await sync.SyncPositionsAsync(accountId, backfill: true, ct);
            return Results.Ok(outcome);
        })
        .WithName("TriggerBackfill")
        .WithSummary("Backfill history")
        .WithDescription("Walks Bitunix history back to the account trackedFrom date. Run once after attaching credentials. Safe to re-run: every write is an upsert keyed on the exchange position id.")
        .Produces<SyncOutcome>();

        group.MapPost("/accounts/{accountId:guid}/snapshot", async (
            Guid accountId,
            BitunixSyncService sync,
            CancellationToken ct) =>
        {
            var snapshot = await sync.CaptureBalanceSnapshotAsync(accountId, ct);
            return snapshot is null
                ? Results.NotFound()
                : Results.Ok(BalanceSnapshotResponse.From(snapshot));
        })
        .WithName("CaptureBalanceSnapshot")
        .WithSummary("Capture a balance snapshot now")
        .WithDescription("Reads the Bitunix futures wallet and records an equity point. Bonus is excluded from equity because it is not withdrawable and would inflate the curve.")
        .Produces<BalanceSnapshotResponse>()
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}

public sealed record SyncStatusResponse(
    Guid AccountId,
    string Endpoint,
    DateTimeOffset? LastSyncedAt,
    DateTimeOffset? LastRecordAt,
    bool BackfillComplete);
