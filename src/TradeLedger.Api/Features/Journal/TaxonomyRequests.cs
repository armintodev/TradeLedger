using TradeLedger.Core.Domain;

namespace TradeLedger.Api.Features.Journal;

public sealed record CreateTaxonomyTermRequest(
    TaxonomyKind Kind,
    string Name,
    int? SortOrder,
    string? ColorHex,
    string? Description
);
