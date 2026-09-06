using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
        .WithDescription("The journal, newest first, with paging and filters. Each row carries both halves: the mechanical fields from the exchange and whatever subjective fields have been filled in so far.")
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
            var trade = await db.Trades.AsNoTracking()
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

            return trade is null ? Results.NotFound() : Results.Ok(TradeDetailResponse.From(trade));
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
            CancellationToken ct) =>
        {
            var trade = await db.Trades
                .Include(t => t.Mistakes)
                .Include(t => t.Trackings)
                .FirstOrDefaultAsync(t => t.Id == id, ct);

            if (trade is null)
            {
                return Results.NotFound();
            }

            trade.StrategyId = request.StrategyId ?? trade.StrategyId;
            trade.TimeframeId = request.TimeframeId ?? trade.TimeframeId;
            trade.EntryTypeId = request.EntryTypeId ?? trade.EntryTypeId;
            trade.ExitTypeId = request.ExitTypeId ?? trade.ExitTypeId;
            trade.EntryMentalStateId = request.EntryMentalStateId ?? trade.EntryMentalStateId;
            trade.ExitMentalStateId = request.ExitMentalStateId ?? trade.ExitMentalStateId;
            trade.Rating = request.Rating ?? trade.Rating;
            trade.Memo = request.Memo ?? trade.Memo;
            trade.Tag = request.Tag ?? trade.Tag;
            trade.PostTradeTag = request.PostTradeTag ?? trade.PostTradeTag;

            if (request.MarketContext is not null)
            {
                trade.MarketContext = request.MarketContext;
            }

            if (request.StopLossPrice is { } sl)
            {
                trade.StopLossPrice = sl;
            }

            if (request.TakeProfitPrice is { } tp)
            {
                trade.TakeProfitPrice = tp;
            }

            if (request.MistakeIds is not null)
            {
                db.TradeMistakes.RemoveRange(trade.Mistakes);
                foreach (var termId in request.MistakeIds.Distinct())
                {
                    db.TradeMistakes.Add(new TradeMistake
                    {
                        UserId = trade.UserId,
                        TradeId = trade.Id,
                        TaxonomyTermId = termId,
                    });
                }
            }

            if (request.TrackingIds is not null)
            {
                db.TradeTrackings.RemoveRange(trade.Trackings);
                foreach (var termId in request.TrackingIds.Distinct())
                {
                    db.TradeTrackings.Add(new TradeTracking
                    {
                        UserId = trade.UserId,
                        TradeId = trade.Id,
                        TaxonomyTermId = termId,
                    });
                }
            }

            if (request.MarkReviewed == true)
            {
                trade.ReviewState = ReviewState.Reviewed;
            }

            trade.Recalculate();
            await db.SaveChangesAsync(ct);

            var saved = await db.Trades.AsNoTracking()
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
                .FirstAsync(t => t.Id == id, ct);

            return Results.Ok(TradeDetailResponse.From(saved));
        })
        .WithName("JournalTrade")
        .WithSummary("Add the subjective half of a trade")
        .WithDescription("Supplies what the exchange cannot know: strategy, timeframe, entry and exit mental state, market context checklist, mistakes, trackings, rating and memo. Only the fields you send are changed. Setting a stop loss here also recomputes achieved R, since a synced trade has no stop reported by the exchange. Pass markReviewed to clear it from the inbox.")
        .Produces<TradeDetailResponse>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", async (
            [FromBody] CreateManualTradeRequest request,
            TradeLedgerDbContext db,
            CancellationToken ct) =>
        {
            var trade = new Trade
            {
                AccountId = request.AccountId,
                Symbol = request.Symbol,
                Side = request.Side,
                Origin = TradeOrigin.Manual,
                OpenedAt = request.OpenedAt,
                ClosedAt = request.ClosedAt,
                EntryPrice = request.EntryPrice,
                ExitPrice = request.ExitPrice,
                Quantity = request.Quantity,
                Leverage = request.Leverage ?? 1,
                PositionMargin = request.PositionMargin,
                Fees = request.Fees ?? 0m,
                Funding = request.Funding ?? 0m,
                StopLossPrice = request.StopLossPrice,
                TakeProfitPrice = request.TakeProfitPrice,
                StrategyId = request.StrategyId,
                Memo = request.Memo,
            };

            if (trade.ExitPrice is { } exit)
            {
                var direction = trade.Side == TradeSide.Long ? 1m : -1m;
                trade.GrossProfitLoss = (exit - trade.EntryPrice) * trade.Quantity * direction;
            }

            trade.Recalculate();
            db.Trades.Add(trade);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/trades/{trade.Id}", TradeListItem.From(trade));
        })
        .WithName("CreateManualTrade")
        .WithSummary("Record a manual trade")
        .WithDescription("For venues no API reaches: external wallets, other exchanges, on-chain swaps. Gross PnL is derived from the prices you enter, then net PnL, outcome, achieved R and duration are computed the same way as for a synced trade.")
        .Produces<TradeListItem>(StatusCodes.Status201Created);

        group.MapDelete("/{id:guid}", async (Guid id, TradeLedgerDbContext db, CancellationToken ct) =>
        {
            var trade = await db.Trades.FirstOrDefaultAsync(t => t.Id == id, ct);
            if (trade is null)
            {
                return Results.NotFound();
            }

            if (trade.Origin == TradeOrigin.Synced)
            {
                return Results.Problem(
                    title: "Synced trades cannot be deleted",
                    detail: "This trade came from the exchange and would return on the next sync.",
                    statusCode: StatusCodes.Status409Conflict);
            }

            db.Trades.Remove(trade);
            await db.SaveChangesAsync(ct);

            return Results.NoContent();
        })
        .WithName("DeleteTrade")
        .WithSummary("Delete a manual trade")
        .WithDescription("Manual trades only. A synced trade cannot be deleted: it would reappear on the next sync sweep, and deleting it would discard the journalling attached to it. Returns 409 if you try.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status409Conflict);
    }
}

public sealed record PagedResult<T>(List<T> Items, int Page, int PageSize, int Total);

public sealed record TradeListItem(
    Guid Id,
    Guid AccountId,
    string Symbol,
    TradeSide Side,
    TradeOrigin Origin,
    ReviewState ReviewState,
    TradeOutcome Outcome,
    bool IsPlanned,
    DateTimeOffset OpenedAt,
    DateTimeOffset? ClosedAt,
    decimal EntryPrice,
    decimal? ExitPrice,
    decimal Quantity,
    int Leverage,
    decimal Fees,
    decimal Funding,
    decimal NetProfitLoss,
    decimal? AchievedReturnR,
    decimal? PlannedReturnR,
    string? StrategyName,
    int? Rating,
    TimeSpan? Duration)
{
    public static TradeListItem From(Trade t) => new(
        t.Id, t.AccountId, t.Symbol, t.Side, t.Origin, t.ReviewState, t.Outcome, t.IsPlanned,
        t.OpenedAt, t.ClosedAt, t.EntryPrice, t.ExitPrice, t.Quantity, t.Leverage,
        t.Fees, t.Funding, t.NetProfitLoss, t.AchievedReturnR, t.PlannedReturnR,
        t.Strategy != null ? t.Strategy.Name : null, t.Rating, t.Duration);
}

public sealed record JournalTradeRequest(
    Guid? StrategyId,
    Guid? TimeframeId,
    Guid? EntryTypeId,
    Guid? ExitTypeId,
    Guid? EntryMentalStateId,
    Guid? ExitMentalStateId,
    MarketContext? MarketContext,
    decimal? StopLossPrice,
    decimal? TakeProfitPrice,
    List<Guid>? MistakeIds,
    List<Guid>? TrackingIds,
    int? Rating,
    string? Memo,
    string? Tag,
    string? PostTradeTag,
    bool? MarkReviewed);

public sealed record CreateManualTradeRequest(
    Guid AccountId,
    string Symbol,
    TradeSide Side,
    DateTimeOffset OpenedAt,
    DateTimeOffset? ClosedAt,
    decimal EntryPrice,
    decimal? ExitPrice,
    decimal Quantity,
    int? Leverage,
    decimal? PositionMargin,
    decimal? Fees,
    decimal? Funding,
    decimal? StopLossPrice,
    decimal? TakeProfitPrice,
    Guid? StrategyId,
    string? Memo);
