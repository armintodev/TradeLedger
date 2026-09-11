namespace TradeLedger.Core.Domain;

public sealed class Execution : IUserOwned
{
    private Execution()
    {
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid UserId { get; private set; }

    public Guid TradeId { get; private set; }

    public Trade? Trade { get; private set; }

    public ExecutionRole Role { get; private set; }

    public TradeSide Side { get; private set; }

    public decimal Price { get; private set; }

    public decimal Quantity { get; private set; }

    public decimal Fee { get; private set; }

    public string FeeAsset { get; private set; } = "USDT";

    public decimal? RealizedProfitLoss { get; private set; }

    public DateTimeOffset ExecutedAt { get; private set; }

    public long? ExecutedAtRawMs { get; private set; }

    public string? ExchangeTradeId { get; private set; }

    public string? ExchangeOrderId { get; private set; }

    public OrderType OrderType { get; private set; } = OrderType.Unknown;

    public Guid AccountId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    public bool Reduces => Role is ExecutionRole.Reduce or ExecutionRole.Close or ExecutionRole.Liquidation;

    public decimal Notional => Price * Quantity;

    public static Execution Record(NewExecution spec) => new()
    {
        UserId = spec.UserId,
        TradeId = Guard.NotEmpty(spec.TradeId, nameof(spec.TradeId)),
        AccountId = Guard.NotEmpty(spec.AccountId, nameof(spec.AccountId)),
        Role = spec.Role,
        Side = spec.Side,
        Price = Guard.Positive(spec.Price, nameof(spec.Price)),
        Quantity = Guard.Positive(spec.Quantity, nameof(spec.Quantity)),
        Fee = Guard.NotNegative(spec.Fee, nameof(spec.Fee)),
        FeeAsset = string.IsNullOrWhiteSpace(spec.FeeAsset) ? "USDT" : spec.FeeAsset,
        RealizedProfitLoss = spec.RealizedProfitLoss,
        ExecutedAt = spec.ExecutedAt,
        ExecutedAtRawMs = spec.ExecutedAtRawMs,
        ExchangeTradeId = spec.ExchangeTradeId,
        ExchangeOrderId = spec.ExchangeOrderId,
        OrderType = spec.OrderType,
    };
}

public sealed record NewExecution
{
    public Guid UserId { get; init; }

    public required Guid TradeId { get; init; }

    public required Guid AccountId { get; init; }

    public required ExecutionRole Role { get; init; }

    public required TradeSide Side { get; init; }

    public required decimal Price { get; init; }

    public required decimal Quantity { get; init; }

    public decimal Fee { get; init; }

    public string? FeeAsset { get; init; }

    public decimal? RealizedProfitLoss { get; init; }

    public required DateTimeOffset ExecutedAt { get; init; }

    public long? ExecutedAtRawMs { get; init; }

    public string? ExchangeTradeId { get; init; }

    public string? ExchangeOrderId { get; init; }

    public OrderType OrderType { get; init; } = OrderType.Unknown;
}
