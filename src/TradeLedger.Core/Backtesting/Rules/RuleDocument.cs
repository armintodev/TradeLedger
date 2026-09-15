using TradeLedger.Core.Backtesting.Indicators;
using TradeLedger.Core.Domain.MarketData;

namespace TradeLedger.Core.Backtesting.Rules;

public enum RuleOperator
{
    And = 1,
    Or = 2,
    Not = 3,
    GreaterThan = 4,
    GreaterOrEqual = 5,
    LessThan = 6,
    LessOrEqual = 7,
    EqualTo = 8,
    NotEqualTo = 9,
    CrossesAbove = 10,
    CrossesBelow = 11,
    Between = 12,
    RisingFor = 13,
    FallingFor = 14,
}

public abstract record RuleOperand;

public sealed record IndicatorOperand(string Ref, string? Output, int Offset) : RuleOperand;

public sealed record PriceOperand(PriceSource Price, int Offset) : RuleOperand;

public sealed record ConstantOperand(decimal Value) : RuleOperand;

public abstract record RuleNode;

public sealed record LogicalNode(RuleOperator Op, IReadOnlyList<RuleNode> Operands) : RuleNode;

public sealed record NotNode(RuleNode Operand) : RuleNode;

public sealed record ComparisonNode(RuleOperator Op, RuleOperand Left, RuleOperand Right) : RuleNode;

public sealed record BetweenNode(RuleOperand Left, RuleOperand Low, RuleOperand High) : RuleNode;

public sealed record TrendNode(RuleOperator Op, RuleOperand Operand, int Bars) : RuleNode;

public abstract record StopLossRule;

public sealed record PercentStop(decimal Percent) : StopLossRule;

public sealed record IndicatorLevelStop(
    string Ref,
    string? Output,
    int Offset,
    decimal BufferPercent) : StopLossRule;

/// <summary>
/// One declared indicator. <paramref name="Interval"/> is null when it reads the interval
/// the run trades, which is every document written before rule version 2.
/// </summary>
public sealed record IndicatorSpec(
    string Id,
    string Type,
    PriceSource Source,
    int Period,
    CandleInterval? Interval);

public sealed record EntryRules(RuleNode? Long, RuleNode? Short);

public sealed record RuleDocument(
    int Version,
    IReadOnlyList<IndicatorSpec> Indicators,
    EntryRules Entry,
    StopLossRule StopLoss)
{
    public const int MinVersion = 1;

    public const int SupportedVersion = 2;

    /// <summary>The version from which an indicator may name an interval of its own.</summary>
    public const int MultiTimeframeVersion = 2;

    /// <summary>
    /// Warmup counted in each indicator's own bars, ignoring which interval it reads.
    /// Honest only for a single-timeframe document; once any indicator names an interval
    /// this under-reports, and <see cref="WarmupBarsFor"/> is the number that matters.
    /// </summary>
    public int WarmupBars => Indicators.Count == 0
        ? 0
        : Indicators.Max(i => IndicatorFactory.WarmupBars(i.Type, i.Period));

    /// <summary>Whether any indicator reads a timeframe other than the run's own.</summary>
    public bool IsMultiTimeframe => Indicators.Any(i => i.Interval is not null);

    /// <summary>The interval a spec reads, given the interval the run trades.</summary>
    public static CandleInterval IntervalOf(IndicatorSpec spec, CandleInterval runInterval) =>
        spec.Interval ?? runInterval;

    /// <summary>Every distinct interval this document reads.</summary>
    public IReadOnlyList<CandleInterval> IntervalsFor(CandleInterval runInterval) =>
        Indicators.Select(i => IntervalOf(i, runInterval)).Distinct().ToArray();

    /// <summary>
    /// Warmup expressed in <paramref name="runInterval"/> bars. An upper bound — it allows a
    /// whole extra bucket per indicator, which is what an unaligned start costs.
    /// </summary>
    public int WarmupBarsFor(CandleInterval runInterval) =>
        Indicators.Count == 0
            ? 0
            : Indicators.Max(i =>
                (IndicatorFactory.WarmupBars(i.Type, i.Period) + 1)
                * IntervalOf(i, runInterval).RatioTo(runInterval));

    /// <summary>
    /// The instant candles must be loaded from for a run starting at <paramref name="from"/>.
    /// </summary>
    /// <remarks>
    /// A bar count cannot express this. The load start is a single instant, but alignment is
    /// a property of each indicator's own grid, and the indicator needing the most bars is
    /// not necessarily the one whose grid that instant has to land on. Flooring per indicator
    /// and taking the earliest result satisfies every grid at once.
    /// </remarks>
    public DateTimeOffset LoadFrom(CandleInterval runInterval, DateTimeOffset from)
    {
        var earliest = runInterval.AlignFloor(from);

        foreach (var spec in Indicators)
        {
            var interval = IntervalOf(spec, runInterval);
            var bars = IndicatorFactory.WarmupBars(spec.Type, spec.Period) + 1;
            var span = TimeSpan.FromTicks(interval.Duration().Ticks * bars);
            var floor = interval.AlignFloor(from);

            // No candle predates the epoch, and a span that reaches past it means the run
            // has no hope of coverage anyway. Clamping keeps the arithmetic from throwing.
            var start = floor - DateTimeOffset.UnixEpoch > span
                ? floor - span
                : DateTimeOffset.UnixEpoch;

            if (start < earliest)
            {
                earliest = start;
            }
        }

        return earliest;
    }
}

public sealed class RuleValidationException(string path, string message)
    : Exception($"{path}: {message}")
{
    public string Path { get; } = path;

    public string Reason { get; } = message;
}
