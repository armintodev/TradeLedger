using TradeLedger.Core.Backtesting.Indicators;

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

public sealed record IndicatorSpec(string Id, string Type, PriceSource Source, int Period);

public sealed record EntryRules(RuleNode? Long, RuleNode? Short);

public sealed record RuleDocument(
    int Version,
    IReadOnlyList<IndicatorSpec> Indicators,
    EntryRules Entry,
    StopLossRule StopLoss)
{
    public const int SupportedVersion = 1;

    public int WarmupBars => Indicators.Count == 0
        ? 0
        : Indicators.Max(i => IndicatorFactory.WarmupBars(i.Type, i.Period));
}

public sealed class RuleValidationException(string path, string message)
    : Exception($"{path}: {message}")
{
    public string Path { get; } = path;

    public string Reason { get; } = message;
}
