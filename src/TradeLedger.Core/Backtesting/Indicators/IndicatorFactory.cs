using TradeLedger.Core.Domain.MarketData;
using TradeLedger.Core.MarketData;

namespace TradeLedger.Core.Backtesting.Indicators;

public sealed record IndicatorDefinition(
    string Type,
    IReadOnlyList<string> Outputs,
    bool TakesSource,
    int WarmupMultiplier);

public static class IndicatorFactory
{
    public const int MinPeriod = 1;
    public const int MaxPeriod = 1000;

    public const string PlusDi = "PlusDi";
    public const string MinusDi = "MinusDi";

    private static readonly Dictionary<string, IndicatorDefinition> Definitions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Sma"] = new("Sma", [IndicatorSeries.DefaultOutput], true, 1),
            ["Ema"] = new("Ema", [IndicatorSeries.DefaultOutput], true, 3),
            ["Rsi"] = new("Rsi", [IndicatorSeries.DefaultOutput], true, 5),
            ["Dmi"] = new("Dmi", [PlusDi, MinusDi], false, 5),
            ["Adx"] = new("Adx", [IndicatorSeries.DefaultOutput], false, 5),
        };

    public static IReadOnlyCollection<string> SupportedTypes => Definitions.Keys;

    public static bool IsKnown(string type) => Definitions.ContainsKey(type);

    public static IndicatorDefinition? Describe(string type) =>
        Definitions.TryGetValue(type, out var definition) ? definition : null;

    public static bool TakesSource(string type) =>
        Definitions.TryGetValue(type, out var definition) && definition.TakesSource;

    public static IReadOnlyList<string> OutputsFor(string type) =>
        Definitions.TryGetValue(type, out var definition)
            ? definition.Outputs
            : throw new ArgumentException($"Unknown indicator type '{type}'.", nameof(type));

    public static bool HasOutput(string type, string? output)
    {
        if (!Definitions.TryGetValue(type, out var definition))
        {
            return false;
        }

        if (output is null)
        {
            return definition.Outputs.Count == 1;
        }

        return definition.Outputs.Contains(output, StringComparer.OrdinalIgnoreCase);
    }

    public static int WarmupBars(string type, int period)
    {
        if (!Definitions.TryGetValue(type, out var definition))
        {
            throw new ArgumentException($"Unknown indicator type '{type}'.", nameof(type));
        }

        return definition.WarmupMultiplier * period;
    }

    public static IndicatorSeries Compute(
        string type,
        int period,
        PriceSource source,
        IReadOnlyList<Candle> candles)
    {
        if (!Definitions.TryGetValue(type, out var definition))
        {
            throw new ArgumentException(
                $"Unknown indicator type '{type}'. Supported: {string.Join(", ", Definitions.Keys)}.",
                nameof(type));
        }

        if (period is < MinPeriod or > MaxPeriod)
        {
            throw new ArgumentOutOfRangeException(
                nameof(period), period,
                $"Indicator period must be between {MinPeriod} and {MaxPeriod}.");
        }

        return definition.Type switch
        {
            "Sma" => IndicatorSeries.Single(IndicatorMath.Sma(source.Extract(candles), period)),
            "Ema" => IndicatorSeries.Single(IndicatorMath.Ema(source.Extract(candles), period)),
            "Rsi" => IndicatorSeries.Single(IndicatorMath.Rsi(source.Extract(candles), period)),
            "Adx" => IndicatorSeries.Single(IndicatorMath.Adx(candles, period)),
            "Dmi" => DmiSeries(candles, period),
            _ => throw new ArgumentException($"Unknown indicator type '{type}'.", nameof(type)),
        };
    }

    private static IndicatorSeries DmiSeries(IReadOnlyList<Candle> candles, int period)
    {
        var (plus, minus, _) = IndicatorMath.Dmi(candles, period);

        return new IndicatorSeries(
            candles.Count,
            new Dictionary<string, decimal?[]>
            {
                [PlusDi] = plus,
                [MinusDi] = minus,
            });
    }
}
