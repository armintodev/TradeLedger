using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TradeLedger.Api.Shared;
using TradeLedger.Core.Domain;
using TradeLedger.Core.Persistence;

namespace TradeLedger.Api.Features.Trades;

public static class TradeEndpoints
{
    public static void MapTradeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/trades").WithTags("Trades").RequireAuthorization();

        group.MapGet("/", async (
            TradeLedgerDbContext db,
            [FromQuery] Guid? accountId,
            [FromQuery] string? symbol,
            [FromQuery] ReviewState? reviewState,
            [FromQuery] MarketSession? marketSession,
            [FromQuery] DateTimeOffset? from,
            [FromQuery] DateTimeOffset? to,
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            CancellationToken ct) =>
        {
            var pageNumber = Math.Max(1, page ?? 1);
            var size = Math.Clamp(pageSize ?? 50, 1, 200);

            var query = db.Trades.AsNoTracking()
                .Include(t => t.Strategy)
                .Include(t => t.Timeframe)
                .AsQueryable();

            if (accountId is { } a)
            {
                query = query.Where(t => t.AccountId == a);
            }

            if (!string.IsNullOrWhiteSpace(symbol))
            {
                query = query.Where(t => t.Symbol == symbol);
            }

            if (reviewState is { } rs)
            {
                query = query.Where(t => t.ReviewState == rs);
            }

            if (marketSession is { } session)
            {
                query = query.Where(t => t.MarketSession == session);
            }

            if (from is { } f)
            {
                query = query.Where(t => t.OpenedAt >= f);
            }

            if (to is { } t2)
            {
                query = query.Where(t => t.OpenedAt <= t2);
            }

            var total = await query.CountAsync(ct);
            var items = await query
                .OrderByDescending(t => t.OpenedAt)
                .Skip((pageNumber - 1) * size)
                .Take(size)
                .Select(t => TradeListItem.From(t))
                .ToListAsync(ct);

            return Results.Ok(new PagedResult<TradeListItem>(items, pageNumber, size, total));
        })
        .WithName("ListTrades")
        .WithSummary("List journal trades")
        .WithDescription("The journal, newest first, with paging and filters. Each row carries both halves: the mechanical fields from the exchange and whatever subjective fields have been filled in so far. Filter by marketSession to compare how the Tokyo, London and New York sessions treat you.")
        .Produces<PagedResult<TradeListItem>>();

        group.MapGet("/inbox", async (TradeLedgerDbContext db, CancellationToken ct) =>
        {
            var items = await db.Trades.AsNoTracking()
                .Where(t => t.ReviewState == ReviewState.Unreviewed
                            && t.Outcome != TradeOutcome.Open)
                .OrderByDescending(t => t.ClosedAt)
                .Take(200)
                .Select(t => TradeListItem.From(t))
                .ToListAsync(ct);

            return Results.Ok(items);
        })
        .WithName("GetReviewInbox")
        .WithSummary("Review inbox")
        .WithDescription("Closed trades that still need their subjective half: strategy, mental state, checklist, mistakes, rating. This is the queue the trader works through after the market closes.")
        .Produces<List<TradeListItem>>();

        group.MapGet("/{id:guid}", async (Guid id, TradeLedgerDbContext db, CancellationToken ct) =>
        {
            var trade = await LoadDetailAsync(db.Trades.AsNoTracking(), id, ct)
                ?? throw new ResourceNotFoundException("Trade", id);

            return Results.Ok(TradeDetailResponse.From(trade));
        })
        .WithName("GetTrade")
        .WithSummary("Get one trade in full")
        .WithDescription("The complete journal row including individual fills, tagged mistakes and trackings, and attachments.")
        .Produces<TradeDetailResponse>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPatch("/{id:guid}/journal", async (
            Guid id,
            [FromBody] JournalTradeRequest request,
            TradeLedgerDbContext db,
            IUnitOfWork unitOfWork,
            CancellationToken ct) =>
        {
            await unitOfWork.ExecuteInTransactionAsync(
                async token =>
                {
                    var trade = await db.Trades
                        .Include(t => t.Mistakes)
                        .Include(t => t.Trackings)
                        .FirstOrDefaultAsync(t => t.Id == id, token)
                        ?? throw new ResourceNotFoundException("Trade", id);

                    trade.Journal(request.ToEdit());

                    if (request.MistakeIds is not null)
                    {
                        trade.ReplaceMistakes(request.MistakeIds);
                    }

                    if (request.TrackingIds is not null)
                    {
                        trade.ReplaceTrackings(request.TrackingIds);
                    }
                },
                ct);

            var saved = await LoadDetailAsync(db.Trades.AsNoTracking(), id, ct)
                ?? throw new ResourceNotFoundException("Trade", id);

            return Results.Ok(TradeDetailResponse.From(saved));
        })
        .WithName("JournalTrade")
        .WithSummary("Add the subjective half of a trade")
        .WithDescription("Supplies what the exchange cannot know: strategy, timeframe, entry and exit mental state, market context checklist, mistakes, trackings, rating and memo. Only the fields you send are changed. Setting a stop loss here also recomputes achieved R, since a synced trade has no stop reported by the exchange. Pass markReviewed to clear it from the inbox. The trade, its mistakes and its trackings are rewritten in one transaction, so a rejected tag cannot leave the row half-updated.")
        .Produces<TradeDetailResponse>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/", async (
            [FromBody] CreateManualTradeRequest request,
            TradeLedgerDbContext db,
            CancellationToken ct) =>
        {
            var trade = Trade.OpenManual(request.ToSpec());

            db.Trades.Add(trade);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/trades/{trade.Id}", TradeListItem.From(trade));
        })
        .WithName("CreateManualTrade")
        .WithSummary("Record a manual trade")
        .WithDescription("For venues no API reaches: external wallets, other exchanges, on-chain swaps. Gross PnL is derived from the prices you enter, then net PnL, outcome, achieved R, market session and duration are computed the same way as for a synced trade. A stop or target on the wrong side of the entry is rejected rather than silently producing a nonsense R multiple.")
        .Produces<TradeListItem>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapDelete("/{id:guid}", async (Guid id, TradeLedgerDbContext db, CancellationToken ct) =>
        {
            var trade = await db.Trades.FirstOrDefaultAsync(t => t.Id == id, ct)
                ?? throw new ResourceNotFoundException("Trade", id);

            trade.EnsureDeletable();

            db.Trades.Remove(trade);
            await db.SaveChangesAsync(ct);

            return Results.NoContent();
        })
        .WithName("DeleteTrade")
        .WithSummary("Delete a manual trade")
        .WithDescription("Manual trades only. A synced trade cannot be deleted: it would reappear on the next sync sweep, and deleting it would discard the journalling attached to it. Returns 409 if you try.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static Task<Trade?> LoadDetailAsync(IQueryable<Trade> trades, Guid id, CancellationToken ct) =>
        trades
            .Include(t => t.Executions)
            .Include(t => t.Mistakes).ThenInclude(m => m.Term)
            .Include(t => t.Trackings).ThenInclude(x => x.Term)
            .Include(t => t.Attachments)
            .Include(t => t.Strategy)
            .Include(t => t.Timeframe)
            .Include(t => t.EntryType)
            .Include(t => t.ExitType)
            .Include(t => t.EntryMentalState)
            .Include(t => t.ExitMentalState)
            .FirstOrDefaultAsync(t => t.Id == id, ct);
}
