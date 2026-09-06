using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TradeLedger.Core.Domain;
using TradeLedger.Core.Persistence;

namespace TradeLedger.Api.Features.Portfolio;

public static class PortfolioEndpoints
{
    public static void MapPortfolioEndpoints(this IEndpointRouteBuilder app)
    {
        var holdings = app.MapGroup("/api/holdings").WithTags("Portfolio").RequireAuthorization();

        holdings.MapGet("/", async (
            TradeLedgerDbContext db,
            [FromQuery] bool? includeClosed,
            CancellationToken ct) =>
        {
            var query = db.Holdings.AsNoTracking().AsQueryable();

            if (includeClosed != true)
            {
                query = query.Where(h => h.ClosedAt == null);
            }

            var rows = await query.OrderBy(h => h.Asset).ToListAsync(ct);
            return Results.Ok(rows.Select(HoldingResponse.From).ToList());
        })
        .WithName("ListHoldings")
        .WithSummary("List holdings")
        .WithDescription("Asset positions that are not round-trips: spot bags, wallet balances, and liquidity pool or farm positions with their APR and farmed status.")
        .Produces<List<HoldingResponse>>();

        holdings.MapPost("/", async (
            [FromBody] CreateHoldingRequest request,
            TradeLedgerDbContext db,
            CancellationToken ct) =>
        {
            var holding = new Holding
            {
                AccountId = request.AccountId,
                Kind = request.Kind,
                Asset = request.Asset,
                Quantity = request.Quantity,
                AverageEntryPrice = request.AverageEntryPrice,
                EntryValueUsd = request.EntryValueUsd,
                PoolName = request.PoolName,
                FarmApr = request.FarmApr,
                IsFarmed = request.IsFarmed ?? false,
                OpenedAt = request.OpenedAt,
                Note = request.Note,
            };

            db.Holdings.Add(holding);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/holdings/{holding.Id}", HoldingResponse.From(holding));
        })
        .WithName("CreateHolding")
        .WithSummary("Record a holding")
        .WithDescription("For spot bags, wallet balances and LP or farm positions that no exchange API reports.")
        .Produces<HoldingResponse>(StatusCodes.Status201Created);

        var transfers = app.MapGroup("/api/transfers").WithTags("Portfolio").RequireAuthorization();

        transfers.MapGet("/", async (TradeLedgerDbContext db, CancellationToken ct) =>
        {
            var rows = await db.Transfers.AsNoTracking()
                .OrderByDescending(t => t.OccurredAt)
                .Take(500)
                .ToListAsync(ct);

            return Results.Ok(rows.Select(TransferResponse.From).ToList());
        })
        .WithName("ListTransfers")
        .WithSummary("List transfers")
        .WithDescription("Deposits, withdrawals and moves between accounts, with their fees.")
        .Produces<List<TransferResponse>>();

        transfers.MapPost("/", async (
            [FromBody] CreateTransferRequest request,
            TradeLedgerDbContext db,
            CancellationToken ct) =>
        {
            var transfer = new Transfer
            {
                FromAccountId = request.FromAccountId,
                ToAccountId = request.ToAccountId,
                Direction = request.Direction,
                Asset = request.Asset,
                Amount = request.Amount,
                Fee = request.Fee ?? 0m,
                ValueUsd = request.ValueUsd,
                WriteOff = request.WriteOff ?? false,
                Network = request.Network,
                TxHash = request.TxHash,
                Counterparty = request.Counterparty,
                Note = request.Note,
                OccurredAt = request.OccurredAt,
            };

            db.Transfers.Add(transfer);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/transfers/{transfer.Id}", TransferResponse.From(transfer));
        })
        .WithName("CreateTransfer")
        .WithSummary("Record a transfer")
        .WithDescription("Money moving in, out or between accounts, including money that simply vanished. Set writeOff for funds that were lost rather than delivered, such as a send on the wrong network, so the equity curve shows it honestly.")
        .Produces<TransferResponse>(StatusCodes.Status201Created);

        var snapshots = app.MapGroup("/api/snapshots").WithTags("Portfolio").RequireAuthorization();

        snapshots.MapPost("/", async (
            [FromBody] CreateSnapshotRequest request,
            TradeLedgerDbContext db,
            CancellationToken ct) =>
        {
            var snapshot = new BalanceSnapshot
            {
                AccountId = request.AccountId,
                Asset = request.Asset ?? "USDT",
                WalletBalance = request.WalletBalance,
                Available = request.Available ?? request.WalletBalance,
                UnrealizedPnl = request.UnrealizedPnl ?? 0m,
                Equity = request.WalletBalance + (request.UnrealizedPnl ?? 0m),
                CapturedAt = request.CapturedAt ?? DateTimeOffset.UtcNow,
                IsManual = true,
            };

            db.BalanceSnapshots.Add(snapshot);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/snapshots/{snapshot.Id}", BalanceSnapshotResponse.From(snapshot));
        })
        .WithName("CreateManualSnapshot")
        .WithSummary("Record a manual balance snapshot")
        .WithDescription("An equity mark for an account with no API, read off by hand. Snapshots are the sole source of the equity curve and every drawdown number, so recording them for manual venues keeps the curve whole.")
        .Produces<BalanceSnapshotResponse>(StatusCodes.Status201Created);
    }
}

public sealed record CreateHoldingRequest(
    Guid AccountId,
    HoldingKind Kind,
    string Asset,
    decimal Quantity,
    decimal? AverageEntryPrice,
    decimal? EntryValueUsd,
    string? PoolName,
    decimal? FarmApr,
    bool? IsFarmed,
    DateTimeOffset OpenedAt,
    string? Note);

public sealed record CreateTransferRequest(
    Guid? FromAccountId,
    Guid? ToAccountId,
    TransferDirection Direction,
    string Asset,
    decimal Amount,
    decimal? Fee,
    decimal? ValueUsd,
    bool? WriteOff,
    string? Network,
    string? TxHash,
    string? Counterparty,
    string? Note,
    DateTimeOffset OccurredAt);

public sealed record CreateSnapshotRequest(
    Guid AccountId,
    string? Asset,
    decimal WalletBalance,
    decimal? Available,
    decimal? UnrealizedPnl,
    DateTimeOffset? CapturedAt);
