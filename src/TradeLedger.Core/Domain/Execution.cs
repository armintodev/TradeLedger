namespace TradeLedger.Core.Domain;

public sealed class Execution : IUserOwned
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }
    public Guid TradeId { get; set; }
    public Trade? Trade { get; set; }

    public ExecutionRole Role { get; set; }
    public TradeSide Side { get; set; }

    public decimal Price { get; set; }
    public decimal Quantity { get; set; }
    public decimal Fee { get; set; }
    public string FeeAsset { get; set; } = "USDT";

    public decimal? RealizedProfitLoss { get; set; }

    public DateTimeOffset ExecutedAt { get; set; }

    public long? ExecutedAtRawMs { get; set; }

    public string? ExchangeTradeId { get; set; }

    public string? ExchangeOrderId { get; set; }
    public OrderType OrderType { get; set; } = OrderType.Unknown;

    public Guid AccountId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
