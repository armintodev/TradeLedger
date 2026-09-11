namespace TradeLedger.Core.Domain;

public sealed record NewManualTrade
{
    public Guid UserId { get; init; }

    public required Guid AccountId { get; init; }

    public required string Symbol { get; init; }

    public required TradeSide Side { get; init; }

    public required DateTimeOffset OpenedAt { get; init; }

    public DateTimeOffset? ClosedAt { get; init; }

    public required decimal EntryPrice { get; init; }

    public decimal? ExitPrice { get; init; }

    public required decimal Quantity { get; init; }

    public int Leverage { get; init; } = 1;

    public decimal? PositionMargin { get; init; }

    public decimal Fees { get; init; }

    public decimal Funding { get; init; }

    public decimal? StopLossPrice { get; init; }

    public decimal? TakeProfitPrice { get; init; }

    public Guid? StrategyId { get; init; }

    public string? Memo { get; init; }
}

public sealed record ExchangePositionSnapshot
{
    public required string ExchangePositionId { get; init; }

    public required string Symbol { get; init; }

    public required TradeSide Side { get; init; }

    public required decimal Quantity { get; init; }

    public required decimal EntryPrice { get; init; }

    public decimal? ExitPrice { get; init; }

    public int Leverage { get; init; } = 1;

    public MarginMode? MarginMode { get; init; }

    public PositionMode? PositionMode { get; init; }

    public decimal? LiquidationPrice { get; init; }

    public decimal? LiquidatedQuantity { get; init; }

    public decimal Fees { get; init; }

    public decimal Funding { get; init; }

    public decimal GrossProfitLoss { get; init; }

    public DateTimeOffset? OpenedAt { get; init; }

    public long? OpenedAtRawMs { get; init; }

    public DateTimeOffset? ClosedAt { get; init; }

    public long? ClosedAtRawMs { get; init; }

    public bool StillOpen { get; init; }
}

public sealed record TradeJournalEdit
{
    public Guid? StrategyId { get; init; }

    public Guid? TimeframeId { get; init; }

    public Guid? EntryTypeId { get; init; }

    public Guid? ExitTypeId { get; init; }

    public Guid? EntryMentalStateId { get; init; }

    public Guid? ExitMentalStateId { get; init; }

    public MarketContext? MarketContext { get; init; }

    public decimal? StopLossPrice { get; init; }

    public decimal? TakeProfitPrice { get; init; }

    public int? Rating { get; init; }

    public string? Memo { get; init; }

    public string? Tag { get; init; }

    public string? PostTradeTag { get; init; }

    public bool MarkReviewed { get; init; }
}
