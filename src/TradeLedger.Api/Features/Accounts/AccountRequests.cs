using TradeLedger.Core.Domain;

namespace TradeLedger.Api.Features.Accounts;

public sealed record CreateAccountRequest(
    string Name,
    AccountKind Kind,
    Venue Venue,
    SyncMode SyncMode,
    string? QuoteAsset,
    DateTimeOffset? TrackedFrom);

public sealed record SetCredentialRequest(string ApiKey, string ApiSecret, string? Label);
