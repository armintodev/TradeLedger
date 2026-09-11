using TradeLedger.Core.Domain.MarketData;

namespace TradeLedger.Core.Domain.Backtesting;

public sealed class BacktestRun : IUserOwned
{
    private readonly List<BacktestTrade> _trades = [];
    private readonly List<BacktestEquityPoint> _equityPoints = [];

    private BacktestRun()
    {
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid UserId { get; private set; }

    public Guid BacktestAccountId { get; private set; }

    public BacktestAccount? BacktestAccount { get; private set; }

    public BacktestKind Kind { get; private set; }

    public BacktestStatus Status { get; private set; } = BacktestStatus.Queued;

    public Guid? BacktestStrategyId { get; private set; }

    public BacktestStrategy? BacktestStrategy { get; private set; }

    public string? RuleJson { get; private set; }

    public string? RuleHash { get; private set; }

    public string? Symbol { get; private set; }

    public CandleSource? Source { get; private set; }

    public CandleInterval? Interval { get; private set; }

    public DateTimeOffset From { get; private set; }

    public DateTimeOffset To { get; private set; }

    public decimal OpeningBalance { get; private set; }

    public decimal? ClosingBalance { get; private set; }

    public decimal RiskPercentPerPosition { get; private set; }

    public decimal RiskRewardRatio { get; private set; }

    public int Leverage { get; private set; }

    public decimal TakerFeeRate { get; private set; }

    public decimal MakerFeeRate { get; private set; }

    public decimal SlippageRate { get; private set; }

    public decimal MaintenanceMarginRate { get; private set; }

    public bool IncludeFunding { get; private set; } = true;

    public string ParametersJson { get; private set; } = "{}";

    public string? WhatIfJson { get; private set; }

    public bool AllowGaps { get; private set; }

    public DataQuality DataQuality { get; private set; } = DataQuality.Clean;

    public int EngineVersion { get; private set; }

    public int TotalBars { get; private set; }

    public int BarsProcessed { get; private set; }

    public decimal ProgressPercent { get; private set; }

    public bool CancellationRequested { get; private set; }

    public DateTimeOffset QueuedAt { get; private set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? FinishedAt { get; private set; }

    public string? Error { get; private set; }

    public string? ResultJson { get; private set; }

    public string? WarningsJson { get; private set; }

    public IReadOnlyCollection<BacktestTrade> Trades => _trades;

    public IReadOnlyCollection<BacktestEquityPoint> EquityPoints => _equityPoints;

    public bool IsQueued => Status == BacktestStatus.Queued;

    public bool IsRunning => Status == BacktestStatus.Running;

    public bool IsFinished => Status is BacktestStatus.Succeeded
        or BacktestStatus.Failed
        or BacktestStatus.Cancelled;

    public bool CountsAgainstAccountBalance => Status is BacktestStatus.Queued
        or BacktestStatus.Running
        or BacktestStatus.Succeeded;

    public TimeSpan? Duration => FinishedAt - StartedAt;

    public static BacktestRun Queue(NewBacktestRun spec)
    {
        Guard.Rule(
            spec.To > spec.From,
            "empty_backtest_window",
            "The backtest window must end after it starts.");

        var run = new BacktestRun
        {
            UserId = spec.UserId,
            BacktestAccountId = Guard.NotEmpty(spec.BacktestAccountId, nameof(spec.BacktestAccountId)),
            Kind = spec.Kind,
            BacktestStrategyId = spec.BacktestStrategyId,
            Symbol = string.IsNullOrWhiteSpace(spec.Symbol) ? null : spec.Symbol.ToUpperInvariant(),
            Source = spec.Kind == BacktestKind.RuleEngine ? spec.Source : null,
            Interval = spec.Kind == BacktestKind.RuleEngine ? spec.Interval : null,
            From = spec.From,
            To = spec.To,
            RiskPercentPerPosition = spec.RiskPercentPerPosition,
            RiskRewardRatio = spec.RiskRewardRatio,
            Leverage = Guard.InRange(spec.Leverage, 1, 500, nameof(spec.Leverage)),
            TakerFeeRate = Guard.NotNegative(spec.TakerFeeRate, nameof(spec.TakerFeeRate)),
            MakerFeeRate = Guard.NotNegative(spec.MakerFeeRate, nameof(spec.MakerFeeRate)),
            SlippageRate = Guard.NotNegative(spec.SlippageRate, nameof(spec.SlippageRate)),
            MaintenanceMarginRate = Guard.NotNegative(
                spec.MaintenanceMarginRate,
                nameof(spec.MaintenanceMarginRate)),
            IncludeFunding = spec.IncludeFunding,
            AllowGaps = spec.AllowGaps,
            DataQuality = spec.AllowGaps ? DataQuality.Gapped : DataQuality.Clean,
            EngineVersion = spec.EngineVersion,
            WhatIfJson = spec.WhatIfJson,
        };

        if (spec.Kind == BacktestKind.RuleEngine)
        {
            Guard.Rule(
                run.Symbol is not null,
                "rule_engine_needs_symbol",
                "A rule engine run needs the symbol it should trade.");

            Guard.Rule(
                run.Interval is not null && CandleIntervals.IsTradeable(run.Interval.Value),
                "untradeable_interval",
                "A rule engine run needs a tradeable interval of fifteen minutes or longer.");
        }

        return run;
    }

    public void UseRules(string ruleJson, string ruleHash)
    {
        RuleJson = Guard.NotBlank(ruleJson, nameof(ruleJson));
        RuleHash = Guard.NotBlank(ruleHash, nameof(ruleHash));
    }

    public void Start(decimal openingBalance)
    {
        Guard.Rule(
            IsQueued,
            "run_not_queued",
            $"Only a queued run can start; this one is {Status}.");

        Status = BacktestStatus.Running;
        StartedAt = DateTimeOffset.UtcNow;
        OpeningBalance = openingBalance;
    }

    public void ReportProgress(int barsProcessed, int totalBars)
    {
        BarsProcessed = barsProcessed;
        TotalBars = totalBars;
        ProgressPercent = totalBars > 0
            ? decimal.Round(Math.Min(barsProcessed, totalBars) / (decimal)totalBars * 100m, 2)
            : 0m;
    }

    public void Succeed(decimal closingBalance, string resultJson, string? warningsJson)
    {
        Status = BacktestStatus.Succeeded;
        ClosingBalance = closingBalance;
        ProgressPercent = 100m;
        FinishedAt = DateTimeOffset.UtcNow;
        ResultJson = resultJson;
        WarningsJson = warningsJson;
    }

    public void Fail(string error)
    {
        Status = BacktestStatus.Failed;
        Error = error;
        FinishedAt = DateTimeOffset.UtcNow;
    }

    public void Cancel()
    {
        Status = BacktestStatus.Cancelled;
        FinishedAt = DateTimeOffset.UtcNow;
    }

    public void RequestCancellation()
    {
        if (IsFinished)
        {
            throw new ResourceConflictException(
                "run_already_finished",
                $"This run already {Status.ToString().ToLowerInvariant()}; there is nothing to cancel.");
        }

        CancellationRequested = true;
    }

    public void MarkDataQuality(DataQuality quality)
    {
        DataQuality = quality;
    }

    public void ReclaimAsQueued()
    {
        Guard.Rule(
            IsRunning,
            "run_not_running",
            "Only a running run can be reclaimed back to the queue.");

        Status = BacktestStatus.Queued;
        StartedAt = null;
        BarsProcessed = 0;
        ProgressPercent = 0m;
    }
}

public sealed record NewBacktestRun
{
    public Guid UserId { get; init; }

    public required Guid BacktestAccountId { get; init; }

    public required BacktestKind Kind { get; init; }

    public Guid? BacktestStrategyId { get; init; }

    public string? Symbol { get; init; }

    public CandleSource? Source { get; init; }

    public CandleInterval? Interval { get; init; }

    public required DateTimeOffset From { get; init; }

    public required DateTimeOffset To { get; init; }

    public required decimal RiskPercentPerPosition { get; init; }

    public required decimal RiskRewardRatio { get; init; }

    public int Leverage { get; init; } = 1;

    public decimal TakerFeeRate { get; init; }

    public decimal MakerFeeRate { get; init; }

    public decimal SlippageRate { get; init; }

    public decimal MaintenanceMarginRate { get; init; }

    public bool IncludeFunding { get; init; } = true;

    public bool AllowGaps { get; init; }

    public int EngineVersion { get; init; }

    public string? WhatIfJson { get; init; }
}
