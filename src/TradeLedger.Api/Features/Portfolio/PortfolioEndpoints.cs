using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TradeLedger.Core.Domain;
using TradeLedger.Core.Persistence;

namespace TradeLedger.Api.Features.Portfolio;

public static class PortfolioEndpoints
{
    public static void MapPortfolioEndpoints(this IEndpointRouteBuilder app)
    {
        MapHoldings(app);
        MapTransfers(app);
        MapSnapshots(app);
    }

    private static void MapHoldings(IEndpointRouteBuilder app)
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

            var rows = await query
                .OrderBy(h => h.Asset)
                .ToListAsync(ct);

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
            var holding = Holding.Open(request.ToSpec());

            db.Holdings.Add(holding);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/holdings/{holding.Id}", HoldingResponse.From(holding));
        })
        .WithName("CreateHolding")
        .WithSummary("Record a holding")
        .WithDescription("For spot bags, wallet balances and LP or farm positions that no exchange API reports.")
        .Produces<HoldingResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        holdings.MapPost("/{id:guid}/reprice", async (
            Guid id,
            [FromBody] RepriceHoldingRequest request,
            TradeLedgerDbContext db,
            CancellationToken ct) =>
        {
            var holding = await db.Holdings.FirstOrDefaultAsync(h => h.Id == id, ct)
                ?? throw new ResourceNotFoundException("Holding", id);

            holding.Reprice(request.Price, request.PricedAt ?? DateTimeOffset.UtcNow);
            await db.SaveChangesAsync(ct);

            return Results.Ok(HoldingResponse.From(holding));
        })
        .WithName("RepriceHolding")
        .WithSummary("Mark a holding to market")
        .WithDescription("Sets the current price and recomputes the current value, so an unrealised gain on a wallet bag is visible without inventing a trade for it.")
        .Produces<HoldingResponse>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        holdings.MapPost("/{id:guid}/close", async (
            Guid id,
            [FromBody] CloseHoldingRequest request,
            TradeLedgerDbContext db,
            CancellationToken ct) =>
        {
            var holding = await db.Holdings.FirstOrDefaultAsync(h => h.Id == id, ct)
                ?? throw new ResourceNotFoundException("Holding", id);

            holding.Close(request.ClosedAt ?? DateTimeOffset.UtcNow);
            await db.SaveChangesAsync(ct);

            return Results.Ok(HoldingResponse.From(holding));
        })
        .WithName("CloseHolding")
        .WithSummary("Close a holding")
        .WithDescription("Marks the position as exited. Closing an already closed holding, or closing it before it opened, is rejected.")
        .Produces<HoldingResponse>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }

    private static void MapTransfers(IEndpointRouteBuilder app)
    {
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
            var transfer = Transfer.Record(request.ToSpec());

            db.Transfers.Add(transfer);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/transfers/{transfer.Id}", TransferResponse.From(transfer));
        })
        .WithName("CreateTransfer")
        .WithSummary("Record a transfer")
        .WithDescription("Money moving in, out or between accounts, including money that simply vanished. Set writeOff for funds that were lost rather than delivered, such as a send on the wrong network, so the equity curve shows it honestly. A deposit must name a destination, a withdrawal a source, and an internal move both.")
        .Produces<TransferResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }

    private static void MapSnapshots(IEndpointRouteBuilder app)
    {
        var snapshots = app.MapGroup("/api/snapshots").WithTags("Portfolio").RequireAuthorization();

        snapshots.MapPost("/", async (
            [FromBody] CreateSnapshotRequest request,
            TradeLedgerDbContext db,
            CancellationToken ct) =>
        {
            var snapshot = BalanceSnapshot.Capture(request.ToSpec());

            db.BalanceSnapshots.Add(snapshot);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/snapshots/{snapshot.Id}", BalanceSnapshotResponse.From(snapshot));
        })
        .WithName("CreateManualSnapshot")
        .WithSummary("Record a manual balance snapshot")
        .WithDescription("An equity mark for an account with no API, read off by hand. Snapshots are the sole source of the equity curve and every drawdown number, so recording them for manual venues keeps the curve whole.")
        .Produces<BalanceSnapshotResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest);
    }
}
