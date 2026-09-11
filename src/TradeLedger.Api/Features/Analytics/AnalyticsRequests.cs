using Microsoft.AspNetCore.Mvc;
using TradeLedger.Core.Analytics;

namespace TradeLedger.Api.Features.Analytics;

public sealed record AnalyticsQuery(
    [FromQuery] Guid? AccountId,
    [FromQuery] DateTimeOffset? From,
    [FromQuery] DateTimeOffset? To,
    [FromQuery] Guid? StrategyId,
    [FromQuery] string? Symbol)
{
    public AnalyticsFilter ToFilter() => new()
    {
        AccountId = AccountId,
        From = From,
        To = To,
        StrategyId = StrategyId,
        Symbol = Symbol,
    };
}
