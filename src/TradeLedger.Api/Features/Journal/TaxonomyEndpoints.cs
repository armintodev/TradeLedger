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
                        .Select(t => TaxonomyTermResponse.From(t))
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
                    var term = TaxonomyTerm.Create(
                        request.Kind,
                        request.Name,
                        request.SortOrder ?? 999,
                        request.ColorHex,
                        request.Description);

                    db.TaxonomyTerms.Add(term);
                    await db.SaveChangesAsync(ct);

                    return Results.Created(
                        $"/api/taxonomy/{term.Id}",
                        TaxonomyTermResponse.From(term));
                }
            )
            .WithName("CreateTaxonomyTerm")
            .WithSummary("Add a vocabulary term")
            .WithDescription("Adds a strategy, mental state, mistake or any other journal term for the current user.")
            .Produces<TaxonomyTermResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost(
                "/{id:guid}/deactivate",
                async (
                    Guid id,
                    TradeLedgerDbContext db,
                    CancellationToken ct) =>
                {
                    var term = await db.TaxonomyTerms.FirstOrDefaultAsync(t => t.Id == id, ct)
                        ?? throw new ResourceNotFoundException("Taxonomy term", id);

                    term.Deactivate();
                    await db.SaveChangesAsync(ct);

                    return Results.NoContent();
                }
            )
            .WithName("DeactivateTaxonomyTerm")
            .WithSummary("Retire a vocabulary term")
            .WithDescription("Deactivates rather than deletes. Historical trades keep their label and the term simply stops appearing in pickers, so past analytics stay intact.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
