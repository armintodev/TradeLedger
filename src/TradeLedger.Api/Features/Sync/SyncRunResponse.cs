using TradeLedger.Core.Domain;

namespace TradeLedger.Api.Features.Sync;

public sealed record SyncRunResponse(
    Guid Id,
    Guid AccountId,
    string Endpoint,
    SyncRunStatus Status,
    bool IsBackfill,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    int RecordsSeen,
    int RecordsWritten,
    int RequestsMade,
    string? Error)
{
    public static SyncRunResponse From(SyncRun r) => new(
        r.Id, r.AccountId, r.Endpoint, r.Status, r.IsBackfill, r.StartedAt, r.FinishedAt,
        r.RecordsSeen, r.RecordsWritten, r.RequestsMade, r.Error);
}
