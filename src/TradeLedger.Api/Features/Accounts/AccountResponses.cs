using TradeLedger.Core.Domain;

namespace TradeLedger.Api.Features.Accounts;

public sealed record AccountResponse(
    Guid Id,
    string Name,
    AccountKind Kind,
    Venue Venue,
    SyncMode SyncMode,
    string QuoteAsset,
    bool IsActive,
    DateTimeOffset? TrackedFrom,
    string? ApiKeyHint,
    bool CredentialEnabled,
    DateTimeOffset? LastVerifiedAt,
    string? VerifiedViaEgress);

public sealed record CreatedAccountResponse(Guid Id);

public sealed record CredentialResponse(string ApiKeyHint, DateTimeOffset? LastVerifiedAt, string? VerifiedViaEgress);
