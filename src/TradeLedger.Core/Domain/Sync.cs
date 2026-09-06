namespace TradeLedger.Core.Domain;

public sealed class SyncCursor : IUserOwned
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }
    public Guid AccountId { get; set; }
    public Account? Account { get; set; }

    public required string Endpoint { get; set; }

    public long? LastRecordMs { get; set; }

    public string? LastRecordId { get; set; }
    public DateTimeOffset? LastSyncedAt { get; set; }

    public bool BackfillComplete { get; set; }

    public long? BackfillCursorMs { get; set; }
}

public sealed class SyncRun : IUserOwned
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }
    public Guid AccountId { get; set; }
    public Account? Account { get; set; }

    public required string Endpoint { get; set; }
    public SyncRunStatus Status { get; set; } = SyncRunStatus.Running;

    public bool IsBackfill { get; set; }

    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? FinishedAt { get; set; }

    public int RecordsSeen { get; set; }
    public int RecordsWritten { get; set; }
    public int RequestsMade { get; set; }

    public string? Error { get; set; }
}

public sealed class RawExchangePayload : IUserOwned
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }
    public Guid AccountId { get; set; }

    public required string Endpoint { get; set; }

    public required string ExternalId { get; set; }

    public required string Payload { get; set; }

    public DateTimeOffset FetchedAt { get; set; } = DateTimeOffset.UtcNow;
}
