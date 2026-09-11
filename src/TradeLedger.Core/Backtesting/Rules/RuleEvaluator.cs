using TradeLedger.Core.Backtesting.Indicators;
using TradeLedger.Core.Domain.MarketData;
using TradeLedger.Core.MarketData;

namespace TradeLedger.Core.Backtesting.Rules;

public sealed class LookaheadException(int requested, int current)
    : Exception(
        $"A rule tried to read bar {requested} while evaluating bar {current}. " +
        "Conditions may only see closed bars at or before the current one."
    );

public sealed class BarWindow
{
    private readonly IReadOnlyList<Candle> _candles;
    private readonly IReadOnlyDictionary<string, IndicatorSeries> _indicators;

    public BarWindow(IReadOnlyList<Candle> candles, IReadOnlyDictionary<string, IndicatorSeries> indicators)
    {
        _candles = candles;
        _indicators = indicators;
        CurrentIndex = -1;
    }

    public int CurrentIndex { get; private set; }

    public int Count => _candles.Count;

    public Candle Current => _candles[CurrentIndex];

    public void MoveTo(int index)
    {
        if (index < 0 || index >= _candles.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(index),
                index,
                "The bar index is outside the loaded candles."
            );
        }

        CurrentIndex = index;
    }

    public decimal? Value(RuleOperand operand, int extraOffset = 0)
    {
        if (extraOffset < 0)
        {
            throw new LookaheadException(CurrentIndex - extraOffset, CurrentIndex);
        }

        switch (operand)
        {
            case ConstantOperand constant:
                return constant.Value;

            case PriceOperand price:
            {
                var index = Resolve(price.Offset + extraOffset);

                return index < 0 ? null : price.Price.ValueOf(_candles[index]);
            }

            case IndicatorOperand indicator:
            {
                var index = Resolve(indicator.Offset + extraOffset);

                if (index < 0)
                {
                    return null;
                }

                return _indicators.TryGetValue(indicator.Ref, out var series)
                    ? series.At(index, indicator.Output)
                    : null;
            }

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(operand),
                    operand,
                    "Unsupported operand."
                );
        }
    }

    private int Resolve(int offset)
    {
        if (offset < 0)
        {
            throw new LookaheadException(CurrentIndex - offset, CurrentIndex);
        }

        var index = CurrentIndex - offset;

        if (index > CurrentIndex)
        {
            throw new LookaheadException(index, CurrentIndex);
        }

        return index;
    }
}

public static class RuleEvaluator
{
    public static bool Evaluate(RuleNode node, BarWindow window) => EvaluateCore(node, window);

    private static bool EvaluateCore(RuleNode node, BarWindow window) => node switch
    {
        LogicalNode logical => EvaluateLogical(logical, window),
        NotNode not => !EvaluateCore(not.Operand, window),
        ComparisonNode comparison => EvaluateComparison(comparison, window),
        BetweenNode between => EvaluateBetween(between, window),
        TrendNode trend => EvaluateTrend(trend, window),
        _ => throw new ArgumentOutOfRangeException(nameof(node), node, "Unsupported condition."),
    };

    private static bool EvaluateLogical(LogicalNode node, BarWindow window)
    {
        if (node.Op == RuleOperator.And)
        {
            foreach (var operand in node.Operands)
            {
                if (!EvaluateCore(operand, window))
                {
                    return false;
                }
            }

            return true;
        }

        foreach (var operand in node.Operands)
        {
            if (EvaluateCore(operand, window))
            {
                return true;
            }
        }

        return false;
    }

    private static bool EvaluateComparison(ComparisonNode node, BarWindow window)
    {
        if (node.Op is RuleOperator.CrossesAbove or RuleOperator.CrossesBelow)
        {
            return EvaluateCross(node, window);
        }

        var left = window.Value(node.Left);
        var right = window.Value(node.Right);

        if (left is null || right is null)
        {
            return false;
        }

        return node.Op switch
        {
            RuleOperator.GreaterThan => left > right,
            RuleOperator.GreaterOrEqual => left >= right,
            RuleOperator.LessThan => left < right,
            RuleOperator.LessOrEqual => left <= right,
            RuleOperator.EqualTo => left == right,
            RuleOperator.NotEqualTo => left != right,
            _ => throw new ArgumentOutOfRangeException(
                nameof(node),
                node.Op,
                "Unsupported comparison."
            ),
        };
    }

    private static bool EvaluateCross(ComparisonNode node, BarWindow window)
    {
        var leftNow = window.Value(node.Left);
        var rightNow = window.Value(node.Right);
        var leftBefore = window.Value(node.Left, 1);
        var rightBefore = window.Value(node.Right, 1);

        if (leftNow is null || rightNow is null || leftBefore is null || rightBefore is null)
        {
            return false;
        }

        return node.Op == RuleOperator.CrossesAbove
            ? leftNow > rightNow && leftBefore <= rightBefore
            : leftNow < rightNow && leftBefore >= rightBefore;
    }

    private static bool EvaluateBetween(BetweenNode node, BarWindow window)
    {
        var value = window.Value(node.Left);
        var low = window.Value(node.Low);
        var high = window.Value(node.High);

        return value is not null
               && low is not null
               && high is not null
               && value >= low
               && value <= high;
    }

    private static bool EvaluateTrend(TrendNode node, BarWindow window)
    {
        for (var step = 0; step < node.Bars; step++)
        {
            var newer = window.Value(node.Operand, step);
            var older = window.Value(node.Operand, step + 1);

            if (newer is null || older is null)
            {
                return false;
            }

            var rising = newer > older;

            if (node.Op == RuleOperator.RisingFor ? !rising : newer >= older)
            {
                return false;
            }
        }

        return true;
    }
}
