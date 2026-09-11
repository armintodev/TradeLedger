namespace TradeLedger.Core.Analytics;

public static class EquityMath
{
    public static EquityCurve Analyse(IReadOnlyList<EquityPoint> points)
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
            Points = [.. points],
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
