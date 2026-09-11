using TradeLedger.Core.Domain.MarketData;
using TradeLedger.Core.MarketData;

namespace TradeLedger.Core.Backtesting.Indicators;

public static class IndicatorMath
{
    public static decimal?[] Sma(decimal[] values, int period)
    {
        Guard(period);

        var result = new decimal?[values.Length];

        if (values.Length < period)
        {
            return result;
        }

        var window = 0m;

        for (var i = 0; i < values.Length; i++)
        {
            window += values[i];

            if (i >= period)
            {
                window -= values[i - period];
            }

            if (i >= period - 1)
            {
                result[i] = window / period;
            }
        }

        return result;
    }

    public static decimal?[] Ema(decimal[] values, int period)
    {
        Guard(period);

        var result = new decimal?[values.Length];

        if (values.Length < period)
        {
            return result;
        }

        var multiplier = 2m / (period + 1);

        var seed = 0m;
        for (var i = 0; i < period; i++)
        {
            seed += values[i];
        }

        var previous = seed / period;
        result[period - 1] = previous;

        for (var i = period; i < values.Length; i++)
        {
            previous = (values[i] - previous) * multiplier + previous;
            result[i] = previous;
        }

        return result;
    }

    public static decimal?[] Rsi(decimal[] values, int period)
    {
        Guard(period);

        var result = new decimal?[values.Length];

        if (values.Length <= period)
        {
            return result;
        }

        var gainSum = 0m;
        var lossSum = 0m;

        for (var i = 1; i <= period; i++)
        {
            var change = values[i] - values[i - 1];

            if (change > 0)
            {
                gainSum += change;
            }
            else
            {
                lossSum += -change;
            }
        }

        var averageGain = gainSum / period;
        var averageLoss = lossSum / period;

        result[period] = RsiFrom(averageGain, averageLoss);

        for (var i = period + 1; i < values.Length; i++)
        {
            var change = values[i] - values[i - 1];
            var gain = change > 0 ? change : 0m;
            var loss = change < 0 ? -change : 0m;

            averageGain = (averageGain * (period - 1) + gain) / period;
            averageLoss = (averageLoss * (period - 1) + loss) / period;

            result[i] = RsiFrom(averageGain, averageLoss);
        }

        return result;
    }

    public static (decimal?[] PlusDi, decimal?[] MinusDi, decimal?[] Dx) Dmi(
        IReadOnlyList<Candle> candles,
        int period)
    {
        Guard(period);

        var count = candles.Count;
        var plusDi = new decimal?[count];
        var minusDi = new decimal?[count];
        var dx = new decimal?[count];

        if (count <= period)
        {
            return (plusDi, minusDi, dx);
        }

        var trSum = 0m;
        var plusSum = 0m;
        var minusSum = 0m;

        for (var i = 1; i <= period; i++)
        {
            var (tr, plusDm, minusDm) = Directional(candles, i);
            trSum += tr;
            plusSum += plusDm;
            minusSum += minusDm;
        }

        Assign(period, trSum, plusSum, minusSum, plusDi, minusDi, dx);

        for (var i = period + 1; i < count; i++)
        {
            var (tr, plusDm, minusDm) = Directional(candles, i);

            trSum = trSum - trSum / period + tr;
            plusSum = plusSum - plusSum / period + plusDm;
            minusSum = minusSum - minusSum / period + minusDm;

            Assign(i, trSum, plusSum, minusSum, plusDi, minusDi, dx);
        }

        return (plusDi, minusDi, dx);
    }

    public static decimal?[] Adx(IReadOnlyList<Candle> candles, int period)
    {
        Guard(period);

        var count = candles.Count;
        var result = new decimal?[count];
        var (_, _, dx) = Dmi(candles, period);

        var firstAdxIndex = 2 * period - 1;

        if (count <= firstAdxIndex)
        {
            return result;
        }

        var sum = 0m;

        for (var i = period; i <= firstAdxIndex; i++)
        {
            sum += dx[i] ?? 0m;
        }

        var previous = sum / period;
        result[firstAdxIndex] = previous;

        for (var i = firstAdxIndex + 1; i < count; i++)
        {
            previous = (previous * (period - 1) + (dx[i] ?? 0m)) / period;
            result[i] = previous;
        }

        return result;
    }

    private static void Assign(
        int index,
        decimal trSum,
        decimal plusSum,
        decimal minusSum,
        decimal?[] plusDi,
        decimal?[] minusDi,
        decimal?[] dx)
    {
        var plus = trSum > 0 ? 100m * plusSum / trSum : 0m;
        var minus = trSum > 0 ? 100m * minusSum / trSum : 0m;
        var total = plus + minus;

        plusDi[index] = plus;
        minusDi[index] = minus;
        dx[index] = total > 0 ? 100m * Math.Abs(plus - minus) / total : 0m;
    }

    private static (decimal Tr, decimal PlusDm, decimal MinusDm) Directional(
        IReadOnlyList<Candle> candles,
        int index)
    {
        var current = candles[index];
        var previous = candles[index - 1];

        var highMove = current.High - previous.High;
        var lowMove = previous.Low - current.Low;

        var plusDm = highMove > lowMove && highMove > 0 ? highMove : 0m;
        var minusDm = lowMove > highMove && lowMove > 0 ? lowMove : 0m;

        var trueRange = Math.Max(
            current.High - current.Low,
            Math.Max(
                Math.Abs(current.High - previous.Close),
                Math.Abs(current.Low - previous.Close)));

        return (trueRange, plusDm, minusDm);
    }

    private static decimal RsiFrom(decimal averageGain, decimal averageLoss)
    {
        if (averageLoss == 0)
        {
            return averageGain == 0 ? 50m : 100m;
        }

        var relativeStrength = averageGain / averageLoss;

        return 100m - 100m / (1m + relativeStrength);
    }

    private static void Guard(int period)
    {
        if (period < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(period), period, "Indicator period must be at least 1.");
        }
    }
}
