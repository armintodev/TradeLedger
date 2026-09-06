namespace TradeLedger.Core.Domain;

public sealed class TradePlan : IUserOwned
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }

    public Guid? AccountId { get; set; }
    public Account? Account { get; set; }

    public required string Symbol { get; set; }
    public TradeSide Side { get; set; }

    public Guid? StrategyId { get; set; }
    public TaxonomyTerm? Strategy { get; set; }

    public Guid? TimeframeId { get; set; }
    public TaxonomyTerm? Timeframe { get; set; }

    public Guid? EntryMentalStateId { get; set; }
    public TaxonomyTerm? EntryMentalState { get; set; }

    public decimal PlannedEntryPrice { get; set; }
    public decimal PlannedStopLossPrice { get; set; }
    public decimal? PlannedTakeProfitPrice { get; set; }

    public decimal RiskFraction { get; set; }

    public decimal PlannedRiskReward { get; set; }
    public int Leverage { get; set; } = 1;
    public decimal? AverageFeeRate { get; set; }

    public decimal? PlannedQuantity { get; set; }
    public decimal? PlannedOrderValue { get; set; }
    public decimal? PlannedMargin { get; set; }
    public decimal? EstimatedProfit { get; set; }
    public decimal? EstimatedLoss { get; set; }

    public decimal? BalanceAtPlanning { get; set; }

    public MarketContext? MarketContext { get; set; }

    public PlanStatus Status { get; set; } = PlanStatus.Draft;
    public string? Notes { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Guid? LinkedTradeId { get; set; }

    public Trade? LinkedTrade { get; set; }
}
