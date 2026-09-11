using TradeLedger.Core.Domain;

namespace TradeLedger.Core.Backtesting.Engine;

public enum SizingRefusal
{
    None = 0,
    NoEquity = 1,
    InvalidStop = 2,
    InsufficientMargin = 3,
}

public sealed record PositionSize(
    decimal Quantity,
    decimal Notional,
    decimal Margin,
    decimal RiskAmount,
    decimal StopDistance);

public sealed record SizingOutcome(PositionSize? Size, SizingRefusal Refusal)
{
    public bool Accepted => Size is not null;

    public static SizingOutcome Refused(SizingRefusal refusal) => new(null, refusal);

    public static SizingOutcome Sized(PositionSize size) => new(size, SizingRefusal.None);
}

public static class PositionSizer
{
    public static SizingOutcome Size(
        decimal equity,
        decimal riskPercent,
        TradeSide side,
        decimal entryPrice,
        decimal stopPrice,
        int leverage)
    {
        if (equity <= 0)
        {
            return SizingOutcome.Refused(SizingRefusal.NoEquity);
        }

        if (!IsStopOnTheCorrectSide(side, entryPrice, stopPrice))
        {
            return SizingOutcome.Refused(SizingRefusal.InvalidStop);
        }

        var stopDistance = Math.Abs(entryPrice - stopPrice);

        if (stopDistance <= 0 || entryPrice <= 0 || leverage < 1)
        {
            return SizingOutcome.Refused(SizingRefusal.InvalidStop);
        }

        var riskAmount = equity * riskPercent / 100m;
        var quantity = riskAmount / stopDistance;
        var notional = quantity * entryPrice;
        var margin = notional / leverage;

        if (margin > equity)
        {
            return SizingOutcome.Refused(SizingRefusal.InsufficientMargin);
        }

        return SizingOutcome.Sized(
            new PositionSize(quantity, notional, margin, riskAmount, stopDistance));
    }

    public static bool IsStopOnTheCorrectSide(TradeSide side, decimal entryPrice, decimal stopPrice) =>
        side == TradeSide.Long ? stopPrice < entryPrice : stopPrice > entryPrice;

    public static decimal TakeProfitPrice(
        TradeSide side,
        decimal entryPrice,
        decimal stopPrice,
        decimal riskRewardRatio)
    {
        var distance = Math.Abs(entryPrice - stopPrice) * riskRewardRatio;

        return side == TradeSide.Long ? entryPrice + distance : entryPrice - distance;
    }
}
