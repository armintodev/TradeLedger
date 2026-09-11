namespace TradeLedger.Core.Domain.Backtesting;

public sealed class BacktestTrade : IUserOwned
{
    private readonly List<BacktestExecution> _executions = [];

    private BacktestTrade()
    {
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid UserId { get; private set; }

    public Guid BacktestRunId { get; private set; }

    public BacktestRun? BacktestRun { get; private set; }

    public int Sequence { get; private set; }

    public string Symbol { get; private set; } = string.Empty;

    public TradeSide Side { get; private set; }

    public DateTimeOffset OpenedAt { get; private set; }

    public DateTimeOffset? ClosedAt { get; private set; }

    public int EntryBarIndex { get; private set; }

    public int ExitBarIndex { get; private set; }

    public int BarsInTrade { get; private set; }

    public decimal EntryPrice { get; private set; }

    public decimal? ExitPrice { get; private set; }

    public decimal Quantity { get; private set; }

    public int Leverage { get; private set; }

    public decimal PositionMargin { get; private set; }

    public decimal OrderValue { get; private set; }

    public decimal StopLossPrice { get; private set; }

    public decimal TakeProfitPrice { get; private set; }

    public decimal LiquidationPrice { get; private set; }

    public decimal GrossProfitLoss { get; private set; }

    public decimal Fees { get; private set; }

    public decimal Funding { get; private set; }

    public decimal NetProfitLoss { get; private set; }

    public decimal? AchievedReturnR { get; private set; }

    public decimal? PlannedReturnR { get; private set; }

    public decimal? TradeGainPercent { get; private set; }

    public decimal BalanceAfter { get; private set; }

    public TimeSpan? Duration { get; private set; }

    public TradeOutcome Outcome { get; private set; } = TradeOutcome.Open;

    public BacktestExitReason? ExitReason { get; private set; }

    public IntrabarResolution IntrabarResolution { get; private set; } = IntrabarResolution.Unambiguous;

    public bool WasLiquidated { get; private set; }

    public decimal? MaeR { get; private set; }

    public decimal? MfeR { get; private set; }

    public Guid? SourceTradeId { get; private set; }

    public string? Notes { get; private set; }

    public IReadOnlyCollection<BacktestExecution> Executions => _executions;

    public bool ExitWasAssumed => IntrabarResolution
        is IntrabarResolution.AssumedWithinMinute
        or IntrabarResolution.AssumedNoMinuteData;

    public static BacktestTrade Close(ClosedBacktestPosition spec)
    {
        var net = spec.GrossProfitLoss - spec.Fees + spec.Funding;

        var trade = new BacktestTrade
        {
            Sequence = spec.Sequence,
            Symbol = spec.Symbol,
            Side = spec.Side,
            OpenedAt = spec.OpenedAt,
            ClosedAt = spec.ClosedAt,
            EntryBarIndex = spec.EntryBarIndex,
            ExitBarIndex = spec.ExitBarIndex,
            BarsInTrade = spec.ExitBarIndex - spec.EntryBarIndex,
            EntryPrice = spec.EntryPrice,
            ExitPrice = spec.ExitPrice,
            Quantity = spec.Quantity,
            Leverage = spec.Leverage,
            PositionMargin = spec.PositionMargin,
            OrderValue = spec.OrderValue,
            StopLossPrice = spec.StopLossPrice,
            TakeProfitPrice = spec.TakeProfitPrice,
            LiquidationPrice = spec.LiquidationPrice,
            GrossProfitLoss = spec.GrossProfitLoss,
            Fees = spec.Fees,
            Funding = spec.Funding,
            NetProfitLoss = net,
            PlannedReturnR = spec.PlannedReturnR,
            BalanceAfter = spec.BalanceAfter,
            Duration = spec.ClosedAt - spec.OpenedAt,
            ExitReason = spec.ExitReason,
            IntrabarResolution = spec.IntrabarResolution,
            WasLiquidated = spec.ExitReason == BacktestExitReason.Liquidation,
            SourceTradeId = spec.SourceTradeId,
            Notes = spec.Notes,
            Outcome = net switch
            {
                > 0 => TradeOutcome.Win,
                < 0 => TradeOutcome.Loss,
                _ => TradeOutcome.Breakeven,
            },
        };

        if (spec.RiskAmount > 0)
        {
            trade.AchievedReturnR = decimal.Round(net / spec.RiskAmount, 6);
            trade.MaeR = decimal.Round(spec.MaxAdverse / spec.RiskAmount, 6);
            trade.MfeR = decimal.Round(spec.MaxFavourable / spec.RiskAmount, 6);
        }

        if (spec.PositionMargin > 0)
        {
            trade.TradeGainPercent = decimal.Round(net / spec.PositionMargin * 100m, 6);
        }

        return trade;
    }

    public void RecordExecution(
        ExecutionRole role,
        decimal price,
        decimal quantity,
        decimal fee,
        DateTimeOffset executedAt,
        int barIndex) =>
        _executions.Add(BacktestExecution.Record(role, price, quantity, fee, executedAt, barIndex));

    public void BelongsTo(BacktestRun run) => BelongsTo(run.UserId, run.Id);

    public void BelongsTo(Guid userId, Guid backtestRunId)
    {
        UserId = userId;
        BacktestRunId = backtestRunId;

        foreach (var execution in _executions)
        {
            execution.BelongsTo(userId, backtestRunId, Id);
        }
    }
}

public sealed record ClosedBacktestPosition
{
    public required int Sequence { get; init; }

    public required string Symbol { get; init; }

    public required TradeSide Side { get; init; }

    public required DateTimeOffset OpenedAt { get; init; }

    public required DateTimeOffset ClosedAt { get; init; }

    public required int EntryBarIndex { get; init; }

    public required int ExitBarIndex { get; init; }

    public required decimal EntryPrice { get; init; }

    public required decimal ExitPrice { get; init; }

    public required decimal Quantity { get; init; }

    public required int Leverage { get; init; }

    public required decimal PositionMargin { get; init; }

    public required decimal OrderValue { get; init; }

    public required decimal StopLossPrice { get; init; }

    public required decimal TakeProfitPrice { get; init; }

    public required decimal LiquidationPrice { get; init; }

    public required decimal GrossProfitLoss { get; init; }

    public required decimal Fees { get; init; }

    public required decimal Funding { get; init; }

    public required decimal RiskAmount { get; init; }

    public required decimal MaxAdverse { get; init; }

    public required decimal MaxFavourable { get; init; }

    public required decimal BalanceAfter { get; init; }

    public required decimal PlannedReturnR { get; init; }

    public required BacktestExitReason ExitReason { get; init; }

    public required IntrabarResolution IntrabarResolution { get; init; }

    public Guid? SourceTradeId { get; init; }

    public string? Notes { get; init; }
}
