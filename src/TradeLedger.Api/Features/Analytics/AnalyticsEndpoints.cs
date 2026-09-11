using Microsoft.AspNetCore.Mvc;
using TradeLedger.Core.Analytics;

namespace TradeLedger.Api.Features.Analytics;

public static class AnalyticsEndpoints
{
    public static void MapAnalyticsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/analytics").WithTags("Analytics").RequireAuthorization();

        group.MapGet("/summary", async (
            AnalyticsService analytics,
            [AsParameters] AnalyticsQuery query,
            CancellationToken ct) =>
            Results.Ok(await analytics.GetSummaryAsync(query.ToFilter(), ct)))
        .WithName("GetPerformanceSummary")
        .WithSummary("Performance summary")
        .WithDescription("Headline metrics over a date range: win rate, gross and net PnL, fees, funding, average win and loss, profit factor, expectancy, planned versus unplanned performance, streaks and average duration. Profit factor is null rather than infinite when there are no losing trades yet.")
        .Produces<PerformanceSummary>();

        group.MapGet("/equity-curve", async (
            AnalyticsService analytics,
            [AsParameters] AnalyticsQuery query,
            CancellationToken ct) =>
            Results.Ok(await analytics.GetEquityCurveAsync(query.ToFilter(), ct)))
        .WithName("GetEquityCurve")
        .WithSummary("Equity curve and drawdown")
        .WithDescription("Built from balance snapshots, never from summing trades, because spot bags, LP positions and transfers do not appear there. Returns the curve plus peak equity, max drawdown with the date it happened, and current drawdown.")
        .Produces<EquityCurve>();

        group.MapGet("/breakdown/{dimension}", async (
            BreakdownDimension dimension,
            AnalyticsService analytics,
            [AsParameters] AnalyticsQuery query,
            CancellationToken ct) =>
            Results.Ok(await analytics.GetBreakdownAsync(dimension, query.ToFilter(), ct)))
        .WithName("GetBreakdown")
        .WithSummary("Performance by dimension")
        .WithDescription("Slices closed trades by one dimension: strategy, symbol, side, timeframe, market session, entry or exit mental state, day of week, hour of day, or planned versus unplanned.")
        .Produces<List<BreakdownRow>>();

        group.MapGet("/mistakes", async (
            AnalyticsService analytics,
            [AsParameters] AnalyticsQuery query,
            CancellationToken ct) =>
            Results.Ok(await analytics.GetMistakeCostsAsync(query.ToFilter(), ct)))
        .WithName("GetMistakeCosts")
        .WithSummary("Mistake frequency and cost")
        .WithDescription("How often each tagged mistake occurred and what it cost, ordered worst first. Uses the explicitly attributed cost where one was given, otherwise the net result of the trades the mistake was tagged on.")
        .Produces<List<MistakeCost>>();
    }
}
