namespace TradeLedger.Core.Analytics;

public sealed class PositionSizeCalculator
{
    public PositionSizeResult Calculate(PositionSizeRequest request)
    {
        if (request.EntryPrice <= 0)
        {
            throw new ArgumentException("Entry price must be positive.", nameof(request));
        }

        if (request.StopLossPrice <= 0)
        {
            throw new ArgumentException("Stop loss must be positive.", nameof(request));
        }

        if (request.EntryPrice == request.StopLossPrice)
        {
            throw new ArgumentException("Stop loss cannot equal entry price.", nameof(request));
        }

        var isLong = request.StopLossPrice < request.EntryPrice;

        var stopDistance = Math.Abs(request.EntryPrice - request.StopLossPrice);
        var stopRatio = stopDistance / request.EntryPrice;

        var riskAmount = request.Balance * request.RiskFraction;

        var feeRate = request.AverageFeeRate ?? 0m;
        var lossPerUnit = stopDistance + (request.EntryPrice + request.StopLossPrice) * feeRate;

        var quantity = lossPerUnit > 0 ? riskAmount / lossPerUnit : 0m;
        var orderValue = quantity * request.EntryPrice;
        var leverage = request.Leverage > 0 ? request.Leverage : 1;
        var margin = orderValue / leverage;

        var takeProfit = isLong
            ? request.EntryPrice + stopDistance * request.RiskReward
            : request.EntryPrice - stopDistance * request.RiskReward;

        var entryFee = orderValue * feeRate;
        var takeProfitFee = quantity * takeProfit * feeRate;
        var stopFee = quantity * request.StopLossPrice * feeRate;

        var grossProfit = quantity * stopDistance * request.RiskReward;
        var grossLoss = quantity * stopDistance;

        return new PositionSizeResult
        {
            Side = isLong ? "Long" : "Short",
            StopToEntryRatio = Round(stopRatio),
            RiskAmount = Round(riskAmount),
            Quantity = Round(quantity),
            OrderValue = Round(orderValue),
            Margin = Round(margin),
            MarginQuantity = Round(leverage > 0 ? quantity / leverage : 0m),
            TakeProfitPrice = Round(takeProfit),
            EstimatedProfit = Round(grossProfit - entryFee - takeProfitFee),
            EstimatedLoss = Round(grossLoss + entryFee + stopFee),
            FeesEntryPlusTakeProfit = Round(entryFee + takeProfitFee),
            FeesEntryPlusStop = Round(entryFee + stopFee),
        };
    }

    private static decimal Round(decimal value) => decimal.Round(value, 12);
}

public sealed record PositionSizeRequest
{
    public required decimal Balance { get; init; }

    public required decimal EntryPrice { get; init; }

    public required decimal StopLossPrice { get; init; }

    public required decimal RiskFraction { get; init; }

    public decimal RiskReward { get; init; } = 2m;

    public int Leverage { get; init; } = 1;

    public decimal? AverageFeeRate { get; init; }
}

public sealed record PositionSizeResult
{
    public required string Side { get; init; }

    public required decimal StopToEntryRatio { get; init; }
    public required decimal RiskAmount { get; init; }
    public required decimal Quantity { get; init; }
    public required decimal OrderValue { get; init; }
    public required decimal Margin { get; init; }
    public required decimal MarginQuantity { get; init; }
    public required decimal TakeProfitPrice { get; init; }
    public required decimal EstimatedProfit { get; init; }
    public required decimal EstimatedLoss { get; init; }
    public required decimal FeesEntryPlusTakeProfit { get; init; }
    public required decimal FeesEntryPlusStop { get; init; }
}
