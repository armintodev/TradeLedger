namespace TradeLedger.Core.Domain.MarketData;

public sealed class CandleImport : IUserOwned
{
    private CandleImport()
    {
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid UserId { get; private set; }

    public string FileName { get; private set; } = string.Empty;

    public long FileBytes { get; private set; }

    public string ContentSha256 { get; private set; } = string.Empty;

    public CandleSource Source { get; private set; }

    public string Symbol { get; private set; } = string.Empty;

    public CandleInterval Interval { get; private set; }

    public int RowsParsed { get; private set; }

    public int RowsInserted { get; private set; }

    public int RowsSkippedAsDuplicate { get; private set; }

    public DateTimeOffset? FirstOpenTime { get; private set; }

    public DateTimeOffset? LastOpenTime { get; private set; }

    public string? Warnings { get; private set; }

    public DateTimeOffset ImportedAt { get; private set; } = DateTimeOffset.UtcNow;

    public bool AddedNothing => RowsInserted == 0;

    public static CandleImport Record(NewCandleImport spec)
    {
        var import = new CandleImport
        {
            UserId = spec.UserId,
            FileName = Guard.NotBlank(spec.FileName, nameof(spec.FileName)),
            FileBytes = spec.FileBytes,
            ContentSha256 = Guard.NotBlank(spec.ContentSha256, nameof(spec.ContentSha256)),
            Source = spec.Source,
            Symbol = Guard.NotBlank(spec.Symbol, nameof(spec.Symbol)).ToUpperInvariant(),
            Interval = spec.Interval,
            RowsParsed = spec.RowsParsed,
            RowsInserted = spec.RowsInserted,
            FirstOpenTime = spec.FirstOpenTime,
            LastOpenTime = spec.LastOpenTime,
            Warnings = spec.Warnings,
        };

        import.RowsSkippedAsDuplicate = Math.Max(0, spec.RowsSkippedAsDuplicate);

        return import;
    }
}

public sealed record NewCandleImport
{
    public Guid UserId { get; init; }

    public required string FileName { get; init; }

    public long FileBytes { get; init; }

    public required string ContentSha256 { get; init; }

    public required CandleSource Source { get; init; }

    public required string Symbol { get; init; }

    public required CandleInterval Interval { get; init; }

    public int RowsParsed { get; init; }

    public int RowsInserted { get; init; }

    public int RowsSkippedAsDuplicate { get; init; }

    public DateTimeOffset? FirstOpenTime { get; init; }

    public DateTimeOffset? LastOpenTime { get; init; }

    public string? Warnings { get; init; }
}
