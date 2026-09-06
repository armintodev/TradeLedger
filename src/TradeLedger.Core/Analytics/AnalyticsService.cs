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

        var closed = trades.Where(t => t.Outcome != TradeOutcome.Open).ToList();
        var wins = closed.Where(t => t.Outcome == TradeOutcome.Win).ToList();
        var losses = closed.Where(t => t.Outcome == TradeOutcome.Loss).ToList();

        var grossWin = wins.Sum(t => t.NetProfitLoss);

        var grossLoss = Math.Abs(losses.Sum(t => t.NetProfitLoss));

        var planned = closed.Where(t => t.IsPlanned).ToList();
        var unplanned = closed.Where(t => !t.IsPlanned).ToList();

        return new PerformanceSummary
        {
            TotalTrades = closed.Count,
            WinningTrades = wins.Count,
            LosingTrades = losses.Count,
            BreakevenTrades = closed.Count(t => t.Outcome == TradeOutcome.Breakeven),
            OpenPositions = trades.Count(t => t.Outcome == TradeOutcome.Open),

            WinRate = closed.Count > 0 ? decimal.Round((decimal)wins.Count / closed.Count * 100m, 2) : 0m,

            GrossProfitLoss = closed.Sum(t => t.GrossProfitLoss),
            TotalFees = closed.Sum(t => t.Fees),
            TotalFunding = closed.Sum(t => t.Funding),
            NetProfitLoss = closed.Sum(t => t.NetProfitLoss),

            AverageWin = wins.Count > 0 ? decimal.Round(grossWin / wins.Count, 8) : 0m,
            AverageLoss = losses.Count > 0 ? decimal.Round(grossLoss / losses.Count, 8) : 0m,

            ProfitFactor = grossLoss > 0 ? decimal.Round(grossWin / grossLoss, 4) : null,

            Expectancy = closed.Count > 0
                ? decimal.Round(closed.Sum(t => t.NetProfitLoss) / closed.Count, 8)
                : 0m,

            AverageAchievedR = closed.Count(t => t.AchievedReturnR is not null) > 0
                ? decimal.Round(
                    closed.Where(t => t.AchievedReturnR is not null).Average(t => t.AchievedReturnR!.Value),
                    4
                )
                : null,

            AveragePlannedR = closed.Count(t => t.PlannedReturnR is not null) > 0
                ? decimal.Round(
                    closed.Where(t => t.PlannedReturnR is not null).Average(t => t.PlannedReturnR!.Value),
                    4
                )
                : null,

            PlannedTradeCount = planned.Count,
            UnplannedTradeCount = unplanned.Count,
            PlannedNetProfitLoss = planned.Sum(t => t.NetProfitLoss),
            UnplannedNetProfitLoss = unplanned.Sum(t => t.NetProfitLoss),

            LongestWinStreak = LongestStreak(closed, TradeOutcome.Win),
            LongestLossStreak = LongestStreak(closed, TradeOutcome.Loss),

            AverageDuration = closed.Count(t => t.Duration is not null) > 0
                ? TimeSpan.FromTicks(
                    (long)closed
                        .Where(t => t.Duration is not null)
                        .Average(t => t.Duration!.Value.Ticks)
                )
                : null,
        };
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

        var peak = 0m;
        var maxDrawdown = 0m;
        var maxDrawdownPercent = 0m;
        DateTimeOffset? maxDrawdownAt = null;

        foreach (var point in points)
        {
            if (point.Equity > peak)
            {
                peak = point.Equity;
            }

            if (peak <= 0)
            {
                continue;
            }

            var drawdown = peak - point.Equity;

            if (drawdown > maxDrawdown)
            {
                maxDrawdown = drawdown;
                maxDrawdownPercent = decimal.Round(drawdown / peak * 100m, 4);
                maxDrawdownAt = point.At;
            }
        }

        var current = points.Count > 0 ? points[^1].Equity : 0m;
        var currentDrawdown = peak > 0 ? peak - current : 0m;

        return new EquityCurve
        {
            Points = points,
            StartEquity = points.Count > 0 ? points[0].Equity : 0m,
            CurrentEquity = current,
            PeakEquity = peak,
            MaxDrawdown = maxDrawdown,
            MaxDrawdownPercent = maxDrawdownPercent,
            MaxDrawdownAt = maxDrawdownAt,
            CurrentDrawdown = currentDrawdown,
            CurrentDrawdownPercent = peak > 0 ? decimal.Round(currentDrawdown / peak * 100m, 4) : 0m,
        };
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

    private static int LongestStreak(IEnumerable<Trade> trades, TradeOutcome outcome)
    {
        var best = 0;
        var current = 0;

        foreach (var trade in trades.OrderBy(t => t.ClosedAt ?? t.OpenedAt))
        {
            if (trade.Outcome == outcome)
            {
                current++;
                best = Math.Max(best, current);
            }
            else
            {
                current = 0;
            }
        }

        return best;
    }
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

public sealed record PerformanceSummary
{
    public int TotalTrades { get; init; }
    public int WinningTrades { get; init; }
    public int LosingTrades { get; init; }
    public int BreakevenTrades { get; init; }
    public int OpenPositions { get; init; }
    public decimal WinRate { get; init; }

    public decimal GrossProfitLoss { get; init; }
    public decimal TotalFees { get; init; }
    public decimal TotalFunding { get; init; }
    public decimal NetProfitLoss { get; init; }

    public decimal AverageWin { get; init; }
    public decimal AverageLoss { get; init; }

    public decimal? ProfitFactor { get; init; }

    public decimal Expectancy { get; init; }
    public decimal? AverageAchievedR { get; init; }
    public decimal? AveragePlannedR { get; init; }

    public int PlannedTradeCount { get; init; }
    public int UnplannedTradeCount { get; init; }
    public decimal PlannedNetProfitLoss { get; init; }
    public decimal UnplannedNetProfitLoss { get; init; }

    public int LongestWinStreak { get; init; }
    public int LongestLossStreak { get; init; }
    public TimeSpan? AverageDuration { get; init; }
}

public sealed record EquityPoint(DateTimeOffset At, decimal Equity);

public sealed record EquityCurve
{
    public required List<EquityPoint> Points { get; init; }
    public decimal StartEquity { get; init; }
    public decimal CurrentEquity { get; init; }
    public decimal PeakEquity { get; init; }
    public decimal MaxDrawdown { get; init; }
    public decimal MaxDrawdownPercent { get; init; }
    public DateTimeOffset? MaxDrawdownAt { get; init; }
    public decimal CurrentDrawdown { get; init; }
    public decimal CurrentDrawdownPercent { get; init; }
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
