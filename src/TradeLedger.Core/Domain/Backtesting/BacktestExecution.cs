namespace TradeLedger.Core.Domain.Backtesting;

public sealed class BacktestExecution : IUserOwned
{
    private BacktestExecution()
    {
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid UserId { get; private set; }

    public Guid BacktestRunId { get; private set; }

    public Guid BacktestTradeId { get; private set; }

    public BacktestTrade? BacktestTrade { get; private set; }

    public ExecutionRole Role { get; private set; }

    public decimal Price { get; private set; }

    public decimal Quantity { get; private set; }

    public decimal Fee { get; private set; }

    public DateTimeOffset ExecutedAt { get; private set; }

    public int BarIndex { get; private set; }

    public decimal Notional => Price * Quantity;

    public static BacktestExecution Record(
        ExecutionRole role,
        decimal price,
        decimal quantity,
        decimal fee,
        DateTimeOffset executedAt,
        int barIndex) => new()
    {
        Role = role,
        Price = price,
        Quantity = quantity,
        Fee = fee,
        ExecutedAt = executedAt,
        BarIndex = barIndex,
    };

    public void BelongsTo(Guid userId, Guid backtestRunId, Guid backtestTradeId)
    {
        UserId = userId;
        BacktestRunId = backtestRunId;
        BacktestTradeId = backtestTradeId;
    }
}
