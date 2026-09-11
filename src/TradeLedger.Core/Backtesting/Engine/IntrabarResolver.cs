using TradeLedger.Core.Domain;
using TradeLedger.Core.Domain.Backtesting;
using TradeLedger.Core.Domain.MarketData;
using TradeLedger.Core.MarketData;

namespace TradeLedger.Core.Backtesting.Engine;

public enum IntrabarLevel
{
    Liquidation = 1,
    Stop = 2,
    Target = 3,
}

public sealed record IntrabarOutcome(IntrabarLevel Level, IntrabarResolution Resolution);

public sealed record IntrabarLevels(decimal Stop, decimal Target, decimal Liquidation);

public static class IntrabarResolver
{
    private static readonly IntrabarLevel[] PessimisticOrder =
    [
        IntrabarLevel.Liquidation,
        IntrabarLevel.Stop,
        IntrabarLevel.Target,
    ];

    public static IntrabarOutcome? Resolve(
        Candle bar,
        TradeSide side,
        IntrabarLevels levels,
        IReadOnlyList<Candle>? minuteBars = null)
    {
        var touched = Reached(bar, side, levels);

        if (touched.Count == 0)
        {
            return null;
        }

        if (touched.Count == 1)
        {
            return new IntrabarOutcome(touched[0], IntrabarResolution.Unambiguous);
        }

        if (minuteBars is null || minuteBars.Count == 0)
        {
            return new IntrabarOutcome(
                Pessimistic(touched), IntrabarResolution.AssumedNoMinuteData);
        }

        foreach (var minute in minuteBars.OrderBy(m => m.OpenTime))
        {
            var inMinute = Reached(minute, side, levels);

            if (inMinute.Count == 0)
            {
                continue;
            }

            return inMinute.Count == 1
                ? new IntrabarOutcome(inMinute[0], IntrabarResolution.ResolvedByMinute)
                : new IntrabarOutcome(
                    Pessimistic(inMinute), IntrabarResolution.AssumedWithinMinute);
        }

        return new IntrabarOutcome(
            Pessimistic(touched), IntrabarResolution.AssumedNoMinuteData);
    }

    public static decimal PriceFor(IntrabarLevel level, IntrabarLevels levels) => level switch
    {
        IntrabarLevel.Stop => levels.Stop,
        IntrabarLevel.Target => levels.Target,
        IntrabarLevel.Liquidation => levels.Liquidation,
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Unknown level."),
    };

    public static decimal FillPrice(
        IntrabarLevel level,
        TradeSide side,
        IntrabarLevels levels,
        Candle bar)
    {
        var price = PriceFor(level, levels);

        if (level == IntrabarLevel.Target)
        {
            return price;
        }

        return side == TradeSide.Long
            ? Math.Min(price, bar.Open)
            : Math.Max(price, bar.Open);
    }

    public static BacktestExitReason ToExitReason(this IntrabarLevel level) => level switch
    {
        IntrabarLevel.Stop => BacktestExitReason.StopLoss,
        IntrabarLevel.Target => BacktestExitReason.TakeProfit,
        IntrabarLevel.Liquidation => BacktestExitReason.Liquidation,
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Unknown level."),
    };

    private static List<IntrabarLevel> Reached(Candle bar, TradeSide side, IntrabarLevels levels)
    {
        var touched = new List<IntrabarLevel>(3);

        if (side == TradeSide.Long)
        {
            if (bar.Low <= levels.Liquidation)
            {
                touched.Add(IntrabarLevel.Liquidation);
            }

            if (bar.Low <= levels.Stop)
            {
                touched.Add(IntrabarLevel.Stop);
            }

            if (bar.High >= levels.Target)
            {
                touched.Add(IntrabarLevel.Target);
            }

            return touched;
        }

        if (bar.High >= levels.Liquidation)
        {
            touched.Add(IntrabarLevel.Liquidation);
        }

        if (bar.High >= levels.Stop)
        {
            touched.Add(IntrabarLevel.Stop);
        }

        if (bar.Low <= levels.Target)
        {
            touched.Add(IntrabarLevel.Target);
        }

        return touched;
    }

    private static IntrabarLevel Pessimistic(List<IntrabarLevel> touched)
    {
        foreach (var level in PessimisticOrder)
        {
            if (touched.Contains(level))
            {
                return level;
            }
        }

        return touched[0];
    }
}
