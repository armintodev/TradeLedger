namespace TradeLedger.Core.Domain.MarketData;

public enum MarketDataJobStatus
{
    Queued = 0,
    Running = 1,
    Succeeded = 2,
    Failed = 3,
    Cancelled = 4,
}

public sealed class MarketDataBackfillJob : IUserOwned
{
    private MarketDataBackfillJob()
    {
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid UserId { get; private set; }

    public CandleSource Source { get; private set; }

    public string Symbol { get; private set; } = string.Empty;

    public CandleInterval Interval { get; private set; }

    public DateTimeOffset From { get; private set; }

    public DateTimeOffset To { get; private set; }

    public bool IncludeFundingRates { get; private set; }

    public MarketDataJobStatus Status { get; private set; } = MarketDataJobStatus.Queued;

    public int CandlesWritten { get; private set; }

    public int FundingRatesWritten { get; private set; }

    public decimal ProgressPercent { get; private set; }

    public bool CancellationRequested { get; private set; }

    public DateTimeOffset QueuedAt { get; private set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? FinishedAt { get; private set; }

    public string? Error { get; private set; }

    public bool IsFinished => Status
        is MarketDataJobStatus.Succeeded
        or MarketDataJobStatus.Failed
        or MarketDataJobStatus.Cancelled;

    public static MarketDataBackfillJob Queue(
        CandleSource source,
        string symbol,
        CandleInterval interval,
        DateTimeOffset from,
        DateTimeOffset to,
        bool includeFundingRates = false,
        Guid userId = default)
    {
        Guard.Rule(to > from, "empty_backfill_window", "The backfill window must end after it starts.");

        Guard.Rule(
            source != CandleSource.CsvImport,
            "csv_cannot_be_backfilled",
            "CsvImport candles are supplied through POST /api/market-data/import, not fetched.");

        return new MarketDataBackfillJob
        {
            UserId = userId,
            Source = source,
            Symbol = Guard.NotBlank(symbol, nameof(symbol)).ToUpperInvariant(),
            Interval = interval,
            From = from,
            To = to,
            IncludeFundingRates = includeFundingRates,
        };
    }

    public void Start()
    {
        Status = MarketDataJobStatus.Running;
        StartedAt = DateTimeOffset.UtcNow;
    }

    public void ReportProgress(int candlesWritten, DateTimeOffset cursor)
    {
        CandlesWritten = candlesWritten;

        var window = (To - From).Ticks;
        var covered = (cursor - From).Ticks;

        ProgressPercent = window > 0
            ? decimal.Round(Math.Clamp((decimal)covered / window, 0m, 1m) * 100m, 2)
            : 100m;
    }

    public void RecordFundingRates(int written)
    {
        FundingRatesWritten = written;
    }

    public void Succeed()
    {
        Status = MarketDataJobStatus.Succeeded;
        ProgressPercent = 100m;
        FinishedAt = DateTimeOffset.UtcNow;
    }

    public void Fail(string error)
    {
        Status = MarketDataJobStatus.Failed;
        Error = error;
        FinishedAt = DateTimeOffset.UtcNow;
    }

    public void Cancel()
    {
        Status = MarketDataJobStatus.Cancelled;
        FinishedAt = DateTimeOffset.UtcNow;
    }

    public void RequestCancellation()
    {
        if (IsFinished)
        {
            throw new ResourceConflictException(
                "job_already_finished",
                $"This backfill already {Status.ToString().ToLowerInvariant()}; there is nothing to cancel.");
        }

        CancellationRequested = true;
    }
}
