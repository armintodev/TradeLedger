using TradeLedger.Core.Analytics;
using TradeLedger.Core.Domain;

namespace TradeLedger.UnitTests.Analytics;

internal static class LegacyAnalyticsReference
{
    public static PerformanceSummary Summary(List<TradeMetricInput> trades)
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

    public static EquityCurve Equity(List<EquityPoint> points)
    {
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
