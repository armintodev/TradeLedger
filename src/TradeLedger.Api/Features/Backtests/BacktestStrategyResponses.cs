using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TradeLedger.Core.Backtesting.Indicators;
using TradeLedger.Core.Backtesting.Rules;
using TradeLedger.Core.Domain;
using TradeLedger.Core.Domain.Backtesting;
using TradeLedger.Core.Persistence;

namespace TradeLedger.Api.Features.Backtests;

public sealed record RuleValidationResponse(
    bool IsValid,
    int? WarmupBars,
    IReadOnlyList<string>? Indicators,
    bool? HasLongEntry,
    bool? HasShortEntry,
    string? Path,
    string? Reason)
{
    public static RuleValidationResponse Valid(RuleDocument document) => new(
        true,
        document.WarmupBars,
        [.. document.Indicators.Select(i => $"{i.Id} ({i.Type} {i.Period})")],
        document.Entry.Long is not null,
        document.Entry.Short is not null,
        null,
        null);

    public static RuleValidationResponse Invalid(RuleValidationException ex) =>
        new(false, null, null, null, null, ex.Path, ex.Reason);
}

public sealed record IndicatorDescriptionResponse(
    string Type,
    IReadOnlyList<string> Outputs,
    bool TakesSource,
    int WarmupMultiplier,
    int MinPeriod,
    int MaxPeriod);

public sealed record BacktestStrategyResponse(
    Guid Id,
    string Name,
    string? Description,
    string RuleHash,
    int Version,
    Guid? StrategyTermId,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    JsonElement? Rule)
{
    public static BacktestStrategyResponse From(BacktestStrategy s, bool includeRule = false) => new(
        s.Id, s.Name, s.Description, s.RuleHash, s.Version, s.StrategyTermId,
        s.IsActive, s.CreatedAt, s.UpdatedAt,
        includeRule ? JsonDocument.Parse(s.RuleJson).RootElement.Clone() : null);
}
