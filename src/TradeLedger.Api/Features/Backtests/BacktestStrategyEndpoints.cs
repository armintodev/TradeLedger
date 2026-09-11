using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TradeLedger.Core.Backtesting.Indicators;
using TradeLedger.Core.Backtesting.Rules;
using TradeLedger.Core.Domain;
using TradeLedger.Core.Domain.Backtesting;
using TradeLedger.Core.Persistence;

namespace TradeLedger.Api.Features.Backtests;

public static class BacktestStrategyEndpoints
{
    public static void MapBacktestStrategyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/backtests/strategies")
            .WithTags("Backtests")
            .RequireAuthorization();

        group.MapGet("/", async (
            TradeLedgerDbContext db,
            [FromQuery] bool? includeInactive,
            CancellationToken ct) =>
        {
            var query = db.BacktestStrategies.AsNoTracking();

            if (includeInactive != true)
            {
                query = query.Where(s => s.IsActive);
            }

            var strategies = await query.OrderBy(s => s.Name).ToListAsync(ct);

            return Results.Ok(strategies.Select(s => BacktestStrategyResponse.From(s)).ToList());
        })
        .WithName("ListBacktestStrategies")
        .WithSummary("List rule strategies")
        .WithDescription("Rule strategies are a separate concept from the Strategy taxonomy on your journal: those are labels, these carry machine-readable entry rules. A strategy may link to a taxonomy term so a backtest can be compared against live trades wearing the same label.")
        .Produces<List<BacktestStrategyResponse>>();

        group.MapPost("/validate", ([FromBody] ValidateRuleRequest request) =>
        {
            try
            {
                var document = RuleDocumentParser.Parse(request.Rule.GetRawText());

                return Results.Ok(RuleValidationResponse.Valid(document));
            }
            catch (RuleValidationException ex)
            {
                return Results.Ok(RuleValidationResponse.Invalid(ex));
            }
        })
        .WithName("ValidateBacktestRule")
        .WithSummary("Check a rule tree without saving it")
        .WithDescription("Returns the warmup bar count a valid rule needs, or the exact JSON path and reason it was rejected. The body is the same shape as POST /api/backtests/strategies, so a rule can be checked and then saved without reshaping it. Useful before committing to a strategy, because warmup silently extends how much history a run requires: a 200-period EMA on 4h candles needs 600 bars before the first signal can be evaluated.")
        .Produces<RuleValidationResponse>();

        group.MapGet("/indicators", () => Results.Ok(
            IndicatorFactory.SupportedTypes
                .Select(t => IndicatorFactory.Describe(t)!)
                .Select(d => new IndicatorDescriptionResponse(
                    d.Type,
                    d.Outputs,
                    d.TakesSource,
                    d.WarmupMultiplier,
                    IndicatorFactory.MinPeriod,
                    IndicatorFactory.MaxPeriod))
                .OrderBy(d => d.Type)
                .ToList()))
        .WithName("ListSupportedIndicators")
        .WithSummary("Indicators a rule may reference")
        .WithDescription("Exactly five are implemented, in decimal arithmetic rather than floating point. RSI, DMI and ADX use Wilder smoothing, which converges slowly, so their warmup is five times the period. Adding another indicator is a code change, not configuration.")
        .Produces<List<IndicatorDescriptionResponse>>();

        group.MapPost("/", async (
            [FromBody] SaveBacktestStrategyRequest request,
            TradeLedgerDbContext db,
            CancellationToken ct) =>
        {
            var json = request.Rule.GetRawText();

            try
            {
                RuleDocumentParser.Parse(json);
            }
            catch (RuleValidationException ex)
            {
                throw RuleProblem(ex);
            }

            var strategy = BacktestStrategy.Create(
                request.Name,
                json,
                RuleDocumentParser.CanonicalHash(json),
                request.Description,
                request.StrategyTermId);

            db.BacktestStrategies.Add(strategy);
            await db.SaveChangesAsync(ct);

            return Results.Created(
                $"/api/backtests/strategies/{strategy.Id}",
                BacktestStrategyResponse.From(strategy));
        })
        .WithName("CreateBacktestStrategy")
        .WithSummary("Create a rule strategy")
        .WithDescription("The rule tree is validated before it is stored, so an unusable strategy never reaches a run. Exits are not declared here: every position closes on its stop, on a target derived at the run's risk-to-reward ratio, or on liquidation.")
        .Produces<BacktestStrategyResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/{id:guid}", async (
            Guid id,
            TradeLedgerDbContext db,
            CancellationToken ct) =>
        {
            var strategy = await db.BacktestStrategies
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == id, ct);

            return strategy is null
                ? throw new ResourceNotFoundException("Backtest strategy", id)
                : Results.Ok(BacktestStrategyResponse.From(strategy, includeRule: true));
        })
        .WithName("GetBacktestStrategy")
        .WithSummary("One strategy with its rule tree")
        .Produces<BacktestStrategyResponse>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] SaveBacktestStrategyRequest request,
            TradeLedgerDbContext db,
            CancellationToken ct) =>
        {
            var strategy = await db.BacktestStrategies.FirstOrDefaultAsync(s => s.Id == id, ct)
                ?? throw new ResourceNotFoundException("Backtest strategy", id);

            var json = request.Rule.GetRawText();

            try
            {
                RuleDocumentParser.Parse(json);
            }
            catch (RuleValidationException ex)
            {
                throw RuleProblem(ex);
            }

            if (!string.IsNullOrWhiteSpace(request.Name))
            {
                strategy.Rename(request.Name);
            }

            if (request.Description is not null)
            {
                strategy.Describe(request.Description);
            }

            if (request.StrategyTermId is not null)
            {
                strategy.BindTerm(request.StrategyTermId);
            }

            strategy.Revise(json, RuleDocumentParser.CanonicalHash(json));

            await db.SaveChangesAsync(ct);

            return Results.Ok(BacktestStrategyResponse.From(strategy, includeRule: true));
        })
        .WithName("UpdateBacktestStrategy")
        .WithSummary("Replace a strategy's rules")
        .WithDescription("Bumps the version. Finished runs are untouched: each stores its own copy of the rule JSON and its hash, so an old result stays explainable after the strategy moves on.")
        .Produces<BacktestStrategyResponse>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", async (
            Guid id,
            TradeLedgerDbContext db,
            CancellationToken ct) =>
        {
            var strategy = await db.BacktestStrategies.FirstOrDefaultAsync(s => s.Id == id, ct)
                ?? throw new ResourceNotFoundException("Backtest strategy", id);

            strategy.Deactivate();
            await db.SaveChangesAsync(ct);

            return Results.NoContent();
        })
        .WithName("DeleteBacktestStrategy")
        .WithSummary("Retire a strategy")
        .WithDescription("A soft delete, because runs reference the strategy. Their stored rule copy means their results remain readable either way.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static DomainValidationException RuleProblem(RuleValidationException ex) =>
        new(ex.Path, ex.Reason);
}
