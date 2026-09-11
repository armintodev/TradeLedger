using TradeLedger.Core.Domain;

namespace TradeLedger.Api.Features.Journal;

public sealed record TaxonomyTermResponse(
    Guid Id,
    TaxonomyKind Kind,
    string Name,
    int SortOrder,
    bool IsActive,
    string? ColorHex,
    string? Description
)
{
    public static TaxonomyTermResponse From(TaxonomyTerm t) => new(
        t.Id, t.Kind, t.Name, t.SortOrder, t.IsActive, t.ColorHex, t.Description);
}
