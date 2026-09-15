using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TradeLedger.Core.Backtesting.Indicators;
using TradeLedger.Core.Backtesting.Rules;
using TradeLedger.Core.Domain;
using TradeLedger.Core.Domain.Backtesting;
using TradeLedger.Core.Domain.MarketData;
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
    public static RuleValidationResponse Valid(RuleDocument document, CandleInterval? interval) => new(
        true,
        WarmupFor(document, interval),
        [.. document.Indicators.Select(Describe)],
        document.Entry.Long is not null,
        document.Entry.Short is not null,
        null,
        null);

    /// <summary>
    /// Null rather than a number when the document reads a higher timeframe and the caller
    /// did not say what it would run at. The unscaled count would under-report by as much as
    /// the interval ratio, and this is the one endpoint whose job is to prevent that.
    /// </summary>
    private static int? WarmupFor(RuleDocument document, CandleInterval? interval)
    {
        if (interval is not { } runInterval)
        {
            return document.IsMultiTimeframe ? null : document.WarmupBars;
        }

        // An interval this document cannot be built from has no warmup answer. Report that
        // as null rather than throwing: validation exists to explain, not to refuse.
        return document.IntervalsFor(runInterval).All(declared => runInterval.DividesInto(declared))
            ? document.WarmupBarsFor(runInterval)
            : null;
    }

    private static string Describe(IndicatorSpec spec) => spec.Interval is { } interval
        ? $"{spec.Id} ({spec.Type} {spec.Period} @ {interval})"
        : $"{spec.Id} ({spec.Type} {spec.Period})";

    public static RuleValidationResponse Invalid(RuleValidationException ex) =>
        new(false, null, null, null, null, ex.Path, ex.Reason);
}

public sealed record IndicatorDescriptionResponse(
    string Type,
    IReadOnlyList<string> Outputs,
    bool TakesSource,
    int WarmupMultiplier,
    int MinPeriod,
    int MaxPeriod,
    string DefaultSource);

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
