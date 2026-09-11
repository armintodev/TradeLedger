namespace TradeLedger.Core.Domain.Backtesting;

public sealed class BacktestEquityPoint : IUserOwned
{
    private BacktestEquityPoint()
    {
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid UserId { get; private set; }

    public Guid BacktestRunId { get; private set; }

    public BacktestRun? BacktestRun { get; private set; }

    public int Sequence { get; private set; }

    public DateTimeOffset At { get; private set; }

    public decimal Equity { get; private set; }

    public decimal Drawdown { get; private set; }

    public decimal DrawdownPercent { get; private set; }

    public bool IsUnderwater => Drawdown > 0m;

    public static BacktestEquityPoint Mark(
        int sequence,
        DateTimeOffset at,
        decimal equity,
        decimal peak)
    {
        var drawdown = Math.Max(0m, peak - equity);

        return new BacktestEquityPoint
        {
            Sequence = sequence,
            At = at,
            Equity = equity,
            Drawdown = drawdown,
            DrawdownPercent = peak > 0m ? decimal.Round(drawdown / peak * 100m, 6) : 0m,
        };
    }

    public void BelongsTo(BacktestRun run) => BelongsTo(run.UserId, run.Id);

    public void BelongsTo(Guid userId, Guid backtestRunId)
    {
        UserId = userId;
        BacktestRunId = backtestRunId;
    }
}
