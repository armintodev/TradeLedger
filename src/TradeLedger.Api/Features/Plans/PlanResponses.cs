using TradeLedger.Api.Features.Trades;
using TradeLedger.Core.Domain;

namespace TradeLedger.Api.Features.Plans;

public sealed record PlanResponse(
    Guid Id,
    Guid? AccountId,
    string Symbol,
    TradeSide Side,
    PlanStatus Status,
    string? StrategyName,
    string? TimeframeName,
    string? EntryMentalStateName,
    decimal PlannedEntryPrice,
    decimal PlannedStopLossPrice,
    decimal? PlannedTakeProfitPrice,
    decimal RiskFraction,
    decimal PlannedRiskReward,
    int Leverage,
    decimal? AverageFeeRate,
    decimal? PlannedQuantity,
    decimal? PlannedOrderValue,
    decimal? PlannedMargin,
    decimal? EstimatedProfit,
    decimal? EstimatedLoss,
    decimal? BalanceAtPlanning,
    MarketContextResponse? MarketContext,
    string? Notes,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset CreatedAt,
    Guid? LinkedTradeId)
{
    public static PlanResponse From(TradePlan p) => new(
        p.Id, p.AccountId, p.Symbol, p.Side, p.Status,
        p.Strategy?.Name, p.Timeframe?.Name, p.EntryMentalState?.Name,
        p.PlannedEntryPrice, p.PlannedStopLossPrice, p.PlannedTakeProfitPrice,
        p.RiskFraction, p.PlannedRiskReward, p.Leverage, p.AverageFeeRate,
        p.PlannedQuantity, p.PlannedOrderValue, p.PlannedMargin,
        p.EstimatedProfit, p.EstimatedLoss, p.BalanceAtPlanning,
        MarketContextResponse.From(p.MarketContext), p.Notes, p.ExpiresAt, p.CreatedAt, p.LinkedTradeId);
}
