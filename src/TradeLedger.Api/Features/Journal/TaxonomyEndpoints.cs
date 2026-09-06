using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TradeLedger.Core.Domain;
using TradeLedger.Core.Persistence;

namespace TradeLedger.Api.Features.Journal;

public static class TaxonomyEndpoints
{
    public static void MapTaxonomyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/taxonomy").WithTags("Journal").RequireAuthorization();

        group.MapGet(
                "/",
                async (
                    TradeLedgerDbContext db,
                    [FromQuery] TaxonomyKind? kind,
                    [FromQuery] bool? includeInactive,
                    CancellationToken ct) =>
                {
                    var query = db.TaxonomyTerms.AsNoTracking().AsQueryable();

                    if (kind is { } k)
                    {
                        query = query.Where(t => t.Kind == k);
                    }

                    if (includeInactive != true)
                    {
                        query = query.Where(t => t.IsActive);
                    }

                    var terms = await query
                        .OrderBy(t => t.Kind).ThenBy(t => t.SortOrder).ThenBy(t => t.Name)
                        .Select(t => new TaxonomyTermResponse(
                                t.Id,
                                t.Kind,
                                t.Name,
                                t.SortOrder,
                                t.IsActive,
                                t.ColorHex,
                                t.Description
                            )
                        )
                        .ToListAsync(ct);

                    return Results.Ok(terms);
                }
            )
            .WithName("ListTaxonomyTerms")
            .WithSummary("List journal vocabularies")
            .WithDescription("The pickers behind the journal form: strategies, mental states, mistakes, trackings, checklist items, timeframes, entry and exit types. Seeded from the owner workbook with spellings preserved verbatim, and editable per user.")
            .Produces<List<TaxonomyTermResponse>>();

        group.MapPost(
                "/",
                async (
                    [FromBody] CreateTaxonomyTermRequest request,
                    TradeLedgerDbContext db,
                    CancellationToken ct) =>
                {
                    var term = new TaxonomyTerm
                    {
                        Kind = request.Kind,
                        Name = request.Name,
                        SortOrder = request.SortOrder ?? 999,
                        ColorHex = request.ColorHex,
                        Description = request.Description,
                    };

                    db.TaxonomyTerms.Add(term);
                    await db.SaveChangesAsync(ct);

                    return Results.Created(
                        $"/api/taxonomy/{term.Id}",
                        new TaxonomyTermResponse(
                            term.Id, term.Kind, term.Name, term.SortOrder,
                            term.IsActive, term.ColorHex, term.Description));
                }
            )
            .WithName("CreateTaxonomyTerm")
            .WithSummary("Add a vocabulary term")
            .WithDescription("Adds a strategy, mental state, mistake or any other journal term for the current user.")
            .Produces<TaxonomyTermResponse>(StatusCodes.Status201Created);

        group.MapPost(
                "/{id:guid}/deactivate",
                async (
                    Guid id,
                    TradeLedgerDbContext db,
                    CancellationToken ct) =>
                {
                    var term = await db.TaxonomyTerms.FirstOrDefaultAsync(t => t.Id == id, ct);

                    if (term is null)
                    {
                        return Results.NotFound();
                    }

                    term.IsActive = false;
                    await db.SaveChangesAsync(ct);

                    return Results.NoContent();
                }
            )
            .WithName("DeactivateTaxonomyTerm")
            .WithSummary("Retire a vocabulary term")
            .WithDescription("Deactivates rather than deletes. Historical trades keep their label and the term simply stops appearing in pickers, so past analytics stay intact.")
            .Produces(StatusCodes.Status204NoContent);
    }
}

public sealed record TaxonomyTermResponse(
    Guid Id,
    TaxonomyKind Kind,
    string Name,
    int SortOrder,
    bool IsActive,
    string? ColorHex,
    string? Description
);

public sealed record CreateTaxonomyTermRequest(
    TaxonomyKind Kind,
    string Name,
    int? SortOrder,
    string? ColorHex,
    string? Description
);
