using Microsoft.EntityFrameworkCore;
using TradeLedger.Core.Domain;
using TradeLedger.Core.Persistence;

namespace TradeLedger.Core.Analytics;

public sealed class AnalyticsService(TradeLedgerDbContext db)
{
    public async Task<PerformanceSummary> GetSummaryAsync(
        AnalyticsFilter filter,
        CancellationToken ct = default)
    {
        var trades = await Query(filter).ToListAsync(ct).ConfigureAwait(false);

        return PerformanceMetrics.Compute([.. trades.Select(ToMetricInput)]);
    }

    public async Task<EquityCurve> GetEquityCurveAsync(
        AnalyticsFilter filter,
        CancellationToken ct = default)
    {
        var query = db.BalanceSnapshots.AsNoTracking();

        if (filter.AccountId is { } accountId)
        {
            query = query.Where(s => s.AccountId == accountId);
        }

        if (filter.From is { } from)
        {
            query = query.Where(s => s.CapturedAt >= from);
        }

        if (filter.To is { } to)
        {
            query = query.Where(s => s.CapturedAt <= to);
        }

        var raw = await query
            .OrderBy(s => s.CapturedAt)
            .Select(s => new { s.CapturedAt, s.Equity })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var points = raw
            .GroupBy(p => p.CapturedAt)
            .OrderBy(g => g.Key)
            .Select(g => new EquityPoint(g.Key, g.Sum(x => x.Equity)))
            .ToList();

        return EquityMath.Analyse(points);
    }

    public async Task<List<BreakdownRow>> GetBreakdownAsync(
        BreakdownDimension dimension,
        AnalyticsFilter filter,
        CancellationToken ct = default)
    {
        var trades = await Query(filter)
            .Where(t => t.Outcome != TradeOutcome.Open)
            .Include(t => t.Strategy)
            .Include(t => t.Timeframe)
            .Include(t => t.EntryMentalState)
            .Include(t => t.ExitMentalState)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return trades
            .GroupBy(t => KeyFor(t, dimension))
            .Select(g => new BreakdownRow
                {
                    Key = g.Key,
                    TradeCount = g.Count(),
                    WinCount = g.Count(t => t.Outcome == TradeOutcome.Win),
                    WinRate = decimal.Round(
                        (decimal)g.Count(t => t.Outcome == TradeOutcome.Win) / g.Count() * 100m,
                        2
                    ),
                    NetProfitLoss = g.Sum(t => t.NetProfitLoss),
                    AverageR = g.Any(t => t.AchievedReturnR is not null)
                        ? decimal.Round(
                            g.Where(t => t.AchievedReturnR is not null).Average(t => t.AchievedReturnR!.Value),
                            4
                        )
                        : null,
                }
            )
            .OrderByDescending(r => r.NetProfitLoss)
            .ToList();
    }

    public async Task<List<MistakeCost>> GetMistakeCostsAsync(
        AnalyticsFilter filter,
        CancellationToken ct = default)
    {
        var rows = await db.TradeMistakes
            .AsNoTracking()
            .Include(m => m.Term)
            .Include(m => m.Trade)
            .Where(m => m.Trade!.Outcome != TradeOutcome.Open)
            .Where(m => filter.From == null || m.Trade!.OpenedAt >= filter.From)
            .Where(m => filter.To == null || m.Trade!.OpenedAt <= filter.To)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return rows
            .GroupBy(m => m.Term?.Name ?? "(unknown)")
            .Select(g => new MistakeCost
                {
                    Mistake = g.Key,
                    Occurrences = g.Count(),

                    TotalCost = g.Sum(m => m.EstimatedCost ?? m.Trade?.NetProfitLoss ?? 0m),
                }
            )
            .OrderBy(r => r.TotalCost)
            .ToList();
    }

    public static TradeMetricInput ToMetricInput(Trade trade) => new(
        trade.OpenedAt,
        trade.ClosedAt,
        trade.GrossProfitLoss,
        trade.Fees,
        trade.Funding,
        trade.NetProfitLoss,
        trade.Outcome,
        trade.AchievedReturnR,
        trade.PlannedReturnR,
        trade.IsPlanned,
        trade.Duration);

    private IQueryable<Trade> Query(AnalyticsFilter filter)
    {
        var query = db.Trades.AsNoTracking();

        if (filter.AccountId is { } accountId)
        {
            query = query.Where(t => t.AccountId == accountId);
        }

        if (filter.From is { } from)
        {
            query = query.Where(t => t.OpenedAt >= from);
        }

        if (filter.To is { } to)
        {
            query = query.Where(t => t.OpenedAt <= to);
        }

        if (filter.StrategyId is { } strategyId)
        {
            query = query.Where(t => t.StrategyId == strategyId);
        }

        if (!string.IsNullOrWhiteSpace(filter.Symbol))
        {
            query = query.Where(t => t.Symbol == filter.Symbol);
        }

        return query;
    }

    private static string KeyFor(Trade trade, BreakdownDimension dimension) => dimension switch
    {
        BreakdownDimension.Strategy => trade.Strategy?.Name ?? "(none)",
        BreakdownDimension.Symbol => trade.Symbol,
        BreakdownDimension.Side => trade.Side.ToString(),
        BreakdownDimension.Timeframe => trade.Timeframe?.Name ?? "(none)",
        BreakdownDimension.MarketSession => trade.MarketContext?.MarketSession ?? "(none)",
        BreakdownDimension.EntryMentalState => trade.EntryMentalState?.Name ?? "(none)",
        BreakdownDimension.ExitMentalState => trade.ExitMentalState?.Name ?? "(none)",
        BreakdownDimension.DayOfWeek => trade.OpenedAt.UtcDateTime.DayOfWeek.ToString(),
        BreakdownDimension.HourOfDay => trade.OpenedAt.UtcDateTime.Hour.ToString("00"),
        BreakdownDimension.Planned => trade.IsPlanned ? "Planned" : "Unplanned",
        _ => "(all)",
    };
}

public sealed record AnalyticsFilter
{
    public Guid? AccountId { get; init; }
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
    public Guid? StrategyId { get; init; }
    public string? Symbol { get; init; }
}

public enum BreakdownDimension
{
    Strategy,
    Symbol,
    Side,
    Timeframe,
    MarketSession,
    EntryMentalState,
    ExitMentalState,
    DayOfWeek,
    HourOfDay,
    Planned,
}

public sealed record BreakdownRow
{
    public required string Key { get; init; }
    public int TradeCount { get; init; }
    public int WinCount { get; init; }
    public decimal WinRate { get; init; }
    public decimal NetProfitLoss { get; init; }
    public decimal? AverageR { get; init; }
}

public sealed record MistakeCost
{
    public required string Mistake { get; init; }
    public int Occurrences { get; init; }
    public decimal TotalCost { get; init; }
}
