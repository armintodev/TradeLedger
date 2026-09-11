using TradeLedger.Core.Domain;

namespace TradeLedger.Core.Analytics;

public readonly record struct TradeMetricInput(
    DateTimeOffset OpenedAt,
    DateTimeOffset? ClosedAt,
    decimal GrossProfitLoss,
    decimal Fees,
    decimal Funding,
    decimal NetProfitLoss,
    TradeOutcome Outcome,
    decimal? AchievedReturnR,
    decimal? PlannedReturnR,
    bool IsPlanned,
    TimeSpan? Duration);

public static class PerformanceMetrics
{
    public static PerformanceSummary Compute(IReadOnlyList<TradeMetricInput> trades)
    {
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

    private static int LongestStreak(IEnumerable<TradeMetricInput> trades, TradeOutcome outcome)
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
