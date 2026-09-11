using TradeLedger.Core.Domain.MarketData;
using TradeLedger.Core.MarketData;

namespace TradeLedger.Core.Backtesting.Indicators;

public enum PriceSource
{
    Close = 0,
    Open = 1,
    High = 2,
    Low = 3,
    Volume = 4,
}

public static class PriceSources
{
    public static decimal ValueOf(this PriceSource source, Candle candle) => source switch
    {
        PriceSource.Close => candle.Close,
        PriceSource.Open => candle.Open,
        PriceSource.High => candle.High,
        PriceSource.Low => candle.Low,
        PriceSource.Volume => candle.Volume,
        _ => throw new ArgumentOutOfRangeException(
            nameof(source), source, "Unsupported price source."),
    };

    public static decimal[] Extract(this PriceSource source, IReadOnlyList<Candle> candles)
    {
        var values = new decimal[candles.Count];

        for (var i = 0; i < candles.Count; i++)
        {
            values[i] = source.ValueOf(candles[i]);
        }

        return values;
    }
}

public sealed class IndicatorSeries
{
    public const string DefaultOutput = "Value";

    private readonly Dictionary<string, decimal?[]> _outputs;

    public IndicatorSeries(int length, IReadOnlyDictionary<string, decimal?[]> outputs)
    {
        Length = length;
        _outputs = new Dictionary<string, decimal?[]>(outputs, StringComparer.OrdinalIgnoreCase);
    }

    public static IndicatorSeries Single(decimal?[] values) =>
        new(values.Length, new Dictionary<string, decimal?[]> { [DefaultOutput] = values });

    public int Length { get; }

    public IReadOnlyCollection<string> Outputs => _outputs.Keys;

    public bool HasOutput(string output) => _outputs.ContainsKey(output);

    public decimal? At(int index, string? output = null)
    {
        if (index < 0 || index >= Length)
        {
            return null;
        }

        var key = output ?? DefaultOutput;

        return _outputs.TryGetValue(key, out var values)
            ? values[index]
            : throw new ArgumentException(
                $"This indicator has no output named '{key}'. Available: {string.Join(", ", _outputs.Keys)}.",
                nameof(output));
    }
}
