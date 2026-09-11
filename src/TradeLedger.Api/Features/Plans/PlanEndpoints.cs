using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TradeLedger.Core.Analytics;
using TradeLedger.Core.Domain;
using TradeLedger.Core.Persistence;

namespace TradeLedger.Api.Features.Plans;

public static class PlanEndpoints
{
    public static void MapPlanEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/plans").WithTags("Plans").RequireAuthorization();

        group.MapPost("/calculate", (
            [FromBody] PositionSizeRequest request,
            PositionSizeCalculator calculator) => Results.Ok(calculator.Calculate(request)))
        .WithName("CalculatePositionSize")
        .WithSummary("Position size calculator")
        .WithDescription("The workbook Calculator sheet as an endpoint, and stateless: sizing a trade should not require committing to it. Sizes the position so a stop-out loses exactly the risk budget, fees included, then reports order value, margin, take profit at the requested R multiple, and estimated profit and loss net of fees. Side is inferred from where the stop sits relative to entry.")
        .Produces<PositionSizeResult>()
        .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/", async (
            TradeLedgerDbContext db,
            [FromQuery] PlanStatus? status,
            CancellationToken ct) =>
        {
            var query = db.TradePlans.AsNoTracking().Include(p => p.Strategy).AsQueryable();

            if (status is { } s)
            {
                query = query.Where(p => p.Status == s);
            }

            var plans = await query
                .OrderByDescending(p => p.CreatedAt)
                .Take(200)
                .ToListAsync(ct);

            return Results.Ok(plans.Select(PlanResponse.From).ToList());
        })
        .WithName("ListPlans")
        .WithSummary("List trade plans")
        .WithDescription("Pre-trade plans, newest first, optionally filtered by status.")
        .Produces<List<PlanResponse>>();

        group.MapPost("/", async (
            [FromBody] CreatePlanRequest request,
            TradeLedgerDbContext db,
            PositionSizeCalculator calculator,
            CancellationToken ct) =>
        {
            var plan = TradePlan.Create(request.ToSpec());

            if (request.Balance > 0)
            {
                var sizing = calculator.Calculate(new PositionSizeRequest
                {
                    Balance = request.Balance,
                    EntryPrice = request.EntryPrice,
                    StopLossPrice = request.StopLossPrice,
                    RiskFraction = request.RiskFraction,
                    RiskReward = request.RiskReward,
                    Leverage = request.Leverage ?? 1,
                    AverageFeeRate = request.AverageFeeRate,
                });

                plan.ApplySizing(new PlanSizing(
                    sizing.Quantity,
                    sizing.OrderValue,
                    sizing.Margin,
                    request.TakeProfitPrice ?? sizing.TakeProfitPrice,
                    sizing.EstimatedProfit,
                    sizing.EstimatedLoss));
            }

            db.TradePlans.Add(plan);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/plans/{plan.Id}", PlanResponse.From(plan));
        })
        .WithName("CreatePlan")
        .WithSummary("Create a trade plan")
        .WithDescription("Records intent before entry: symbol, side, strategy, stop, risk and R:R, plus the market context checklist. Sizing is calculated and stored alongside. When a matching fill arrives from Bitunix the plan is linked automatically and the trade is flagged planned. Plans expire after three days by default so an untaken plan stops competing to match some later, unrelated trade in the same symbol.")
        .Produces<PlanResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/{id:guid}/abandon", async (
            Guid id,
            TradeLedgerDbContext db,
            CancellationToken ct) =>
        {
            var plan = await db.TradePlans.FirstOrDefaultAsync(p => p.Id == id, ct)
                ?? throw new ResourceNotFoundException("Trade plan", id);

            plan.Abandon();
            await db.SaveChangesAsync(ct);

            return Results.NoContent();
        })
        .WithName("AbandonPlan")
        .WithSummary("Abandon a plan")
        .WithDescription("Marks a plan as not taken so it stops competing for matches. A plan already linked to a trade cannot be abandoned.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);
    }
}
