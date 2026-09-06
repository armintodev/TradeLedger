namespace TradeLedger.Core.Domain;

public sealed class Trade : IUserOwned
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }
    public Guid AccountId { get; set; }
    public Account? Account { get; set; }

    public TradeOrigin Origin { get; set; } = TradeOrigin.Manual;
    public ReviewState ReviewState { get; set; } = ReviewState.Unreviewed;

    public string? ExchangePositionId { get; set; }

    public required string Symbol { get; set; }
    public TradeSide Side { get; set; }
    public DateTimeOffset OpenedAt { get; set; }

    public long? OpenedAtRawMs { get; set; }

    public decimal EntryPrice { get; set; }
    public decimal Quantity { get; set; }
    public decimal? PositionMargin { get; set; }
    public int Leverage { get; set; } = 1;
    public decimal? OrderValue { get; set; }
    public MarginMode? MarginMode { get; set; }
    public PositionMode? PositionMode { get; set; }
    public OrderType OrderType { get; set; } = OrderType.Unknown;

    public DateTimeOffset? ClosedAt { get; set; }
    public long? ClosedAtRawMs { get; set; }
    public decimal? ExitPrice { get; set; }

    public decimal? PercentClosed { get; set; }

    public decimal? LiquidatedQuantity { get; set; }
    public decimal? LiquidationPrice { get; set; }

    public decimal? StopLossPrice { get; set; }
    public decimal? TakeProfitPrice { get; set; }
    public decimal? PositionToAccountPercent { get; set; }
    public decimal? PlannedStopLossPercent { get; set; }
    public decimal? AccountRiskedPercent { get; set; }
    public decimal? PlannedReturnR { get; set; }

    public decimal GrossProfitLoss { get; set; }

    public decimal Fees { get; set; }

    public decimal Funding { get; set; }

    public decimal NetProfitLoss { get; set; }
    public decimal? AchievedReturnR { get; set; }
    public decimal? TradeGainPercent { get; set; }
    public decimal? AccountChangePercent { get; set; }

    public decimal? BalanceAfter { get; set; }

    public TimeSpan? Duration { get; set; }
    public TradeOutcome Outcome { get; set; } = TradeOutcome.Open;

    public Guid? StrategyId { get; set; }
    public TaxonomyTerm? Strategy { get; set; }

    public Guid? TimeframeId { get; set; }
    public TaxonomyTerm? Timeframe { get; set; }

    public Guid? EntryTypeId { get; set; }
    public TaxonomyTerm? EntryType { get; set; }

    public Guid? ExitTypeId { get; set; }
    public TaxonomyTerm? ExitType { get; set; }

    public Guid? EntryMentalStateId { get; set; }
    public TaxonomyTerm? EntryMentalState { get; set; }

    public Guid? ExitMentalStateId { get; set; }
    public TaxonomyTerm? ExitMentalState { get; set; }

    public MarketContext? MarketContext { get; set; }

    public int? Rating { get; set; }

    public string? Memo { get; set; }

    public string? Tag { get; set; }

    public string? PostTradeTag { get; set; }

    public Guid? TradePlanId { get; set; }
    public TradePlan? TradePlan { get; set; }

    public bool IsPlanned { get; set; }

    public ICollection<Execution> Executions { get; set; } = [];
    public ICollection<TradeMistake> Mistakes { get; set; } = [];
    public ICollection<TradeTracking> Trackings { get; set; } = [];
    public ICollection<Attachment> Attachments { get; set; } = [];
    public ICollection<FundingPayment> FundingPayments { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public void Recalculate(decimal? balanceBefore = null)
    {
        NetProfitLoss = GrossProfitLoss - Fees + Funding;

        if (ClosedAt is null)
        {
            Outcome = TradeOutcome.Open;
            Duration = null;
        }
        else
        {
            Duration = ClosedAt.Value - OpenedAt;
            Outcome = NetProfitLoss switch
            {
                > 0 => TradeOutcome.Win,
                < 0 => TradeOutcome.Loss,
                _ => TradeOutcome.Breakeven,
            };
        }

        var riskPerUnit = StopLossPrice is { } sl && sl > 0
            ? Math.Abs(EntryPrice - sl)
            : (decimal?)null;

        if (riskPerUnit is { } rpu && rpu > 0 && Quantity > 0)
        {
            var riskAmount = rpu * Quantity;
            if (riskAmount > 0)
            {
                AchievedReturnR = decimal.Round(NetProfitLoss / riskAmount, 6);
            }
        }

        if (PositionMargin is { } margin && margin > 0)
        {
            TradeGainPercent = decimal.Round(NetProfitLoss / margin * 100m, 6);
        }

        if (balanceBefore is { } bb && bb > 0)
        {
            AccountChangePercent = decimal.Round(NetProfitLoss / bb * 100m, 6);
            BalanceAfter = bb + NetProfitLoss;

            if (OrderValue is { } ov)
            {
                PositionToAccountPercent = decimal.Round(ov / bb * 100m, 6);
            }

            if (riskPerUnit is { } r && Quantity > 0)
            {
                AccountRiskedPercent = decimal.Round(r * Quantity / bb * 100m, 6);
            }
        }

        if (StopLossPrice is { } stop && stop > 0 && EntryPrice > 0)
        {
            PlannedStopLossPercent = decimal.Round(Math.Abs(EntryPrice - stop) / EntryPrice * 100m, 6);
        }

        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
