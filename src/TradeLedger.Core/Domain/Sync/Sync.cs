namespace TradeLedger.Core.Domain;

public sealed class SyncCursor : IUserOwned
{
    private SyncCursor()
    {
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid UserId { get; private set; }

    public Guid AccountId { get; private set; }

    public Account? Account { get; private set; }

    public string Endpoint { get; private set; } = string.Empty;

    public long? LastRecordMs { get; private set; }

    public string? LastRecordId { get; private set; }

    public DateTimeOffset? LastSyncedAt { get; private set; }

    public bool BackfillComplete { get; private set; }

    public long? BackfillCursorMs { get; private set; }

    public static SyncCursor For(Account account, string endpoint) => new()
    {
        UserId = account.UserId,
        AccountId = account.Id,
        Endpoint = Guard.NotBlank(endpoint, nameof(endpoint)),
    };

    public void Advance(long? newestRecordMs, string? newestRecordId = null)
    {
        if (newestRecordMs is { } candidate && (LastRecordMs is null || candidate > LastRecordMs))
        {
            LastRecordMs = candidate;
            LastRecordId = newestRecordId ?? LastRecordId;
        }

        LastSyncedAt = DateTimeOffset.UtcNow;
    }

    public void CompleteBackfill()
    {
        BackfillComplete = true;
        BackfillCursorMs = null;
        LastSyncedAt = DateTimeOffset.UtcNow;
    }

    public void RecordBackfillProgress(long cursorMs)
    {
        BackfillCursorMs = cursorMs;
    }

    public long? WatermarkFor(bool backfill, DateTimeOffset? trackedFrom) =>
        backfill ? trackedFrom?.ToUnixTimeMilliseconds() : LastRecordMs;
}

public sealed class SyncRun : IUserOwned
{
    private SyncRun()
    {
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid UserId { get; private set; }

    public Guid AccountId { get; private set; }

    public Account? Account { get; private set; }

    public string Endpoint { get; private set; } = string.Empty;

    public SyncRunStatus Status { get; private set; } = SyncRunStatus.Running;

    public bool IsBackfill { get; private set; }

    public DateTimeOffset StartedAt { get; private set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? FinishedAt { get; private set; }

    public int RecordsSeen { get; private set; }

    public int RecordsWritten { get; private set; }

    public int RequestsMade { get; private set; }

    public string? Error { get; private set; }

    public bool IsFinished => FinishedAt is not null;

    public TimeSpan? Duration => FinishedAt - StartedAt;

    public static SyncRun Start(Account account, string endpoint, bool backfill) => new()
    {
        UserId = account.UserId,
        AccountId = account.Id,
        Endpoint = Guard.NotBlank(endpoint, nameof(endpoint)),
        IsBackfill = backfill,
        Status = SyncRunStatus.Running,
    };

    public void Succeed()
    {
        Status = SyncRunStatus.Succeeded;
    }

    public void Fail(string error)
    {
        Status = SyncRunStatus.Failed;
        Error = error;
    }

    public void Finish(int recordsSeen, int recordsWritten, int requestsMade)
    {
        RecordsSeen = recordsSeen;
        RecordsWritten = recordsWritten;
        RequestsMade = requestsMade;
        FinishedAt = DateTimeOffset.UtcNow;
    }
}

public sealed class RawExchangePayload : IUserOwned
{
    private RawExchangePayload()
    {
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid UserId { get; private set; }

    public Guid AccountId { get; private set; }

    public string Endpoint { get; private set; } = string.Empty;

    public string ExternalId { get; private set; } = string.Empty;

    public string Payload { get; private set; } = string.Empty;

    public DateTimeOffset FetchedAt { get; private set; } = DateTimeOffset.UtcNow;

    public static RawExchangePayload Capture(
        Guid userId,
        Guid accountId,
        string endpoint,
        string externalId,
        string payload) => new()
    {
        UserId = userId,
        AccountId = Guard.NotEmpty(accountId, nameof(accountId)),
        Endpoint = Guard.NotBlank(endpoint, nameof(endpoint)),
        ExternalId = Guard.NotBlank(externalId, nameof(externalId)),
        Payload = payload,
    };

    public void Replace(string payload)
    {
        Payload = payload;
        FetchedAt = DateTimeOffset.UtcNow;
    }
}
