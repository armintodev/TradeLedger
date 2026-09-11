using TradeLedger.Api.Features.Trades;
using TradeLedger.Core.Domain;

namespace TradeLedger.Api.Features.Plans;

public sealed record CreatePlanRequest(
    Guid? AccountId,
    string Symbol,
    TradeSide Side,
    decimal EntryPrice,
    decimal StopLossPrice,
    decimal? TakeProfitPrice,
    decimal RiskFraction,
    decimal RiskReward,
    int? Leverage,
    decimal? AverageFeeRate,
    decimal Balance,
    Guid? StrategyId,
    Guid? TimeframeId,
    Guid? EntryMentalStateId,
    MarketContextRequest? MarketContext,
    string? Notes,
    DateTimeOffset? ExpiresAt)
{
    public NewTradePlan ToSpec() => new()
    {
        AccountId = AccountId,
        Symbol = Symbol,
        Side = Side,
        EntryPrice = EntryPrice,
        StopLossPrice = StopLossPrice,
        TakeProfitPrice = TakeProfitPrice,
        RiskFraction = RiskFraction,
        RiskReward = RiskReward,
        Leverage = Leverage ?? 1,
        AverageFeeRate = AverageFeeRate,
        Balance = Balance,
        StrategyId = StrategyId,
        TimeframeId = TimeframeId,
        EntryMentalStateId = EntryMentalStateId,
        MarketContext = MarketContext?.ToDomain(),
        Notes = Notes,
        ExpiresAt = ExpiresAt,
    };
}
