using TradeLedger.Core.Backtesting.Indicators;
using TradeLedger.Core.Domain.MarketData;

namespace TradeLedger.Core.MarketStructure.Thresholds;

/// <summary>
/// A reversal threshold of multiplier × ATR, read at the candidate's own bar. Keeping the
/// volatility estimate out here rather than inside the detector is the point of the seam: the
/// two are tuned against different things and are worth testing apart.
/// </summary>
public sealed class AtrReversalThreshold : IReversalThreshold
{
    private readonly decimal?[] _atr;
    private readonly decimal _multiplier;

    public AtrReversalThreshold(decimal?[] atr, decimal multiplier)
    {
        ArgumentNullException.ThrowIfNull(atr);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(multiplier);

        _atr = atr;
        _multiplier = multiplier;

        var first = 0;

        while (first < atr.Length && atr[first] is null)
        {
            first++;
        }

        FirstAvailableIndex = first;
    }

    public static AtrReversalThreshold From(
        IReadOnlyList<Candle> candles,
        int period,
        decimal multiplier) =>
        new(IndicatorMath.Atr(candles, period), multiplier);

    public int FirstAvailableIndex { get; }

    public decimal? For(int anchorIndex, decimal anchorPrice) =>
        anchorIndex >= 0 && anchorIndex < _atr.Length
            ? _atr[anchorIndex] * _multiplier
            : null;
}
