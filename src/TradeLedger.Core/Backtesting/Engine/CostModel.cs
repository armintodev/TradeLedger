using TradeLedger.Core.Domain;

namespace TradeLedger.Core.Backtesting.Engine;

public sealed record CostModel(
    decimal TakerFeeRate,
    decimal MakerFeeRate,
    decimal SlippageRate)
{
    public decimal EntryFillPrice(TradeSide side, decimal price) =>
        side == TradeSide.Long
            ? price * (1m + SlippageRate)
            : price * (1m - SlippageRate);

    public decimal StopFillPrice(TradeSide side, decimal stopPrice) =>
        side == TradeSide.Long
            ? stopPrice * (1m - SlippageRate)
            : stopPrice * (1m + SlippageRate);

    public decimal TakeProfitFillPrice(decimal targetPrice) => targetPrice;

    public decimal LiquidationFillPrice(decimal liquidationPrice) => liquidationPrice;

    public decimal TakerFee(decimal notional) => Math.Abs(notional) * TakerFeeRate;

    public decimal MakerFee(decimal notional) => Math.Abs(notional) * MakerFeeRate;

    public static decimal FundingPayment(TradeSide side, decimal notional, decimal fundingRate)
    {
        var magnitude = Math.Abs(notional) * fundingRate;

        return side == TradeSide.Long ? -magnitude : magnitude;
    }

    public static decimal GrossProfitLoss(
        TradeSide side,
        decimal entryPrice,
        decimal exitPrice,
        decimal quantity) =>
        side == TradeSide.Long
            ? (exitPrice - entryPrice) * quantity
            : (entryPrice - exitPrice) * quantity;
}

public static class LiquidationModel
{
    public static decimal LiquidationPrice(
        TradeSide side,
        decimal entryPrice,
        int leverage,
        decimal maintenanceMarginRate)
    {
        if (leverage < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(leverage), leverage, "Leverage must be at least 1.");
        }

        var buffer = 1m / leverage - maintenanceMarginRate;

        var price = side == TradeSide.Long
            ? entryPrice * (1m - buffer)
            : entryPrice * (1m + buffer);

        return price < 0 ? 0m : price;
    }

    public static bool WouldLiquidateBeforeStop(
        TradeSide side,
        decimal entryPrice,
        decimal stopPrice,
        decimal liquidationPrice) =>
        side == TradeSide.Long
            ? liquidationPrice > stopPrice
            : liquidationPrice < stopPrice;
}
