namespace TradeLedger.Core.Domain;

public sealed class TradePlan : IUserOwned
{
    public static readonly TimeSpan DefaultLifetime = TimeSpan.FromDays(3);

    private TradePlan()
    {
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid UserId { get; private set; }

    public Guid? AccountId { get; private set; }

    public Account? Account { get; private set; }

    public string Symbol { get; private set; } = string.Empty;

    public TradeSide Side { get; private set; }

    public Guid? StrategyId { get; private set; }

    public TaxonomyTerm? Strategy { get; private set; }

    public Guid? TimeframeId { get; private set; }

    public TaxonomyTerm? Timeframe { get; private set; }

    public Guid? EntryMentalStateId { get; private set; }

    public TaxonomyTerm? EntryMentalState { get; private set; }

    public decimal PlannedEntryPrice { get; private set; }

    public decimal PlannedStopLossPrice { get; private set; }

    public decimal? PlannedTakeProfitPrice { get; private set; }

    public decimal RiskFraction { get; private set; }

    public decimal PlannedRiskReward { get; private set; }

    public int Leverage { get; private set; } = 1;

    public decimal? AverageFeeRate { get; private set; }

    public decimal? PlannedQuantity { get; private set; }

    public decimal? PlannedOrderValue { get; private set; }

    public decimal? PlannedMargin { get; private set; }

    public decimal? EstimatedProfit { get; private set; }

    public decimal? EstimatedLoss { get; private set; }

    public decimal? BalanceAtPlanning { get; private set; }

    public MarketContext? MarketContext { get; private set; }

    public PlanStatus Status { get; private set; } = PlanStatus.Draft;

    public string? Notes { get; private set; }

    public DateTimeOffset? ExpiresAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    public Guid? LinkedTradeId { get; private set; }

    public Trade? LinkedTrade { get; private set; }

    public bool IsLinked => Status == PlanStatus.Linked;

    public bool IsOpenForMatching => Status is PlanStatus.Draft or PlanStatus.Active;

    public decimal RiskPerUnit => Math.Abs(PlannedEntryPrice - PlannedStopLossPrice);

    public static TradePlan Create(NewTradePlan spec)
    {
        var plan = new TradePlan
        {
            UserId = spec.UserId,
            AccountId = spec.AccountId,
            Symbol = Guard.NotBlank(spec.Symbol, nameof(spec.Symbol)).ToUpperInvariant(),
            Side = spec.Side,
            StrategyId = spec.StrategyId,
            TimeframeId = spec.TimeframeId,
            EntryMentalStateId = spec.EntryMentalStateId,
            PlannedEntryPrice = Guard.Positive(spec.EntryPrice, nameof(spec.EntryPrice)),
            PlannedStopLossPrice = Guard.Positive(spec.StopLossPrice, nameof(spec.StopLossPrice)),
            RiskFraction = Guard.Positive(spec.RiskFraction, nameof(spec.RiskFraction)),
            PlannedRiskReward = Guard.Positive(spec.RiskReward, nameof(spec.RiskReward)),
            Leverage = Guard.InRange(spec.Leverage, 1, 500, nameof(spec.Leverage)),
            AverageFeeRate = spec.AverageFeeRate,
            BalanceAtPlanning = spec.Balance,
            MarketContext = spec.MarketContext,
            Notes = spec.Notes,
            Status = PlanStatus.Active,
            ExpiresAt = spec.ExpiresAt ?? DateTimeOffset.UtcNow.Add(DefaultLifetime),
        };

        var stopIsOnTheLosingSide = plan.Side == TradeSide.Long
            ? plan.PlannedStopLossPrice < plan.PlannedEntryPrice
            : plan.PlannedStopLossPrice > plan.PlannedEntryPrice;

        Guard.Rule(
            stopIsOnTheLosingSide,
            "stop_on_wrong_side",
            plan.Side == TradeSide.Long
                ? "A long stop loss must sit below the planned entry price."
                : "A short stop loss must sit above the planned entry price.");

        if (spec.TakeProfitPrice is { } target)
        {
            plan.PlannedTakeProfitPrice = Guard.Positive(target, nameof(spec.TakeProfitPrice));
        }

        return plan;
    }

    public void ApplySizing(PlanSizing sizing)
    {
        PlannedQuantity = sizing.Quantity;
        PlannedOrderValue = sizing.OrderValue;
        PlannedMargin = sizing.Margin;
        PlannedTakeProfitPrice ??= sizing.TakeProfitPrice;
        EstimatedProfit = sizing.EstimatedProfit;
        EstimatedLoss = sizing.EstimatedLoss;
    }

    public bool CanMatch(Trade trade) =>
        IsOpenForMatching
        && UserId == trade.UserId
        && string.Equals(Symbol, trade.Symbol, StringComparison.OrdinalIgnoreCase)
        && Side == trade.Side
        && CreatedAt <= trade.OpenedAt
        && (ExpiresAt is null || ExpiresAt >= trade.OpenedAt);

    public void LinkTo(Trade trade)
    {
        Guard.Rule(
            CanMatch(trade),
            "plan_not_matchable",
            "This plan cannot be linked to that trade: it is closed, expired, or for a different market.");

        Status = PlanStatus.Linked;
        LinkedTradeId = trade.Id;
    }

    public void Abandon()
    {
        if (IsLinked)
        {
            throw new ResourceConflictException(
                "plan_already_linked",
                "This plan is already linked to a trade and cannot be abandoned.");
        }

        Status = PlanStatus.Abandoned;
    }

    public void Expire()
    {
        if (IsOpenForMatching)
        {
            Status = PlanStatus.Expired;
        }
    }
}

public sealed record NewTradePlan
{
    public Guid UserId { get; init; }

    public Guid? AccountId { get; init; }

    public required string Symbol { get; init; }

    public required TradeSide Side { get; init; }

    public required decimal EntryPrice { get; init; }

    public required decimal StopLossPrice { get; init; }

    public decimal? TakeProfitPrice { get; init; }

    public required decimal RiskFraction { get; init; }

    public required decimal RiskReward { get; init; }

    public int Leverage { get; init; } = 1;

    public decimal? AverageFeeRate { get; init; }

    public decimal? Balance { get; init; }

    public Guid? StrategyId { get; init; }

    public Guid? TimeframeId { get; init; }

    public Guid? EntryMentalStateId { get; init; }

    public MarketContext? MarketContext { get; init; }

    public string? Notes { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }
}

public readonly record struct PlanSizing(
    decimal Quantity,
    decimal OrderValue,
    decimal Margin,
    decimal TakeProfitPrice,
    decimal EstimatedProfit,
    decimal EstimatedLoss);
