using TradeLedger.Core.Domain.MarketData;
using TradeLedger.Core.MarketStructure.Thresholds;

namespace TradeLedger.Core.MarketStructure;

/// <summary>
/// Turns a chronological candle stream into the swing highs and lows that separate one leg
/// from the next. Stage one of the pipeline in docs/market-structure.md.
/// </summary>
/// <remarks>
/// It decides where price turned and nothing else — not whether the market is bullish, not how
/// big a wave is, not whether momentum is strong, not whether to trade. Those are later stages
/// reading this output.
///
/// Static and stateless like <see cref="Indicators.IndicatorMath"/>, and computed over the
/// whole range in one pass, which is how every other series in the engine is built. The state
/// machine is private and advanced one bar at a time, so the live track can later wrap it in a
/// streaming façade without the algorithm changing.
/// </remarks>
public static class SwingDetector
{
    public static SwingSeries Detect(
        IReadOnlyList<Candle> candles,
        IReversalThreshold threshold)
    {
        ArgumentNullException.ThrowIfNull(candles);
        ArgumentNullException.ThrowIfNull(threshold);

        var start = Math.Max(0, threshold.FirstAvailableIndex);

        if (start >= candles.Count)
        {
            return new SwingSeries([], null, candles.Count);
        }

        var state = new DetectorState(candles[start], start);
        var points = new List<SwingPoint>();

        for (var index = start + 1; index < candles.Count; index++)
        {
            if (state.Advance(candles, index, threshold) is { } point)
            {
                points.Add(point);
            }
        }

        return new SwingSeries(points, state.Pending(candles), start);
    }

    private enum Search
    {
        Unknown = 0,
        High = 1,
        Low = 2,
    }

    /// <summary>
    /// The state machine from docs/market-structure.md. Both candidates are live until the
    /// first confirmation settles a direction; after that only one of them is.
    /// </summary>
    private sealed class DetectorState
    {
        private Search _search = Search.Unknown;

        private decimal _highPrice;
        private int _highIndex;

        private decimal _lowPrice;
        private int _lowIndex;

        public DetectorState(Candle seed, int index)
        {
            _highPrice = seed.High;
            _highIndex = index;
            _lowPrice = seed.Low;
            _lowIndex = index;
        }

        public SwingPoint? Advance(
            IReadOnlyList<Candle> candles,
            int index,
            IReversalThreshold threshold)
        {
            var bar = candles[index];

            return _search switch
            {
                Search.High => AdvanceHigh(candles, bar, index, threshold),
                Search.Low => AdvanceLow(candles, bar, index, threshold),
                _ => AdvanceUnknown(candles, bar, index, threshold),
            };
        }

        public PendingSwing? Pending(IReadOnlyList<Candle> candles) => _search switch
        {
            Search.High => new PendingSwing(
                SwingType.High, _highPrice, _highIndex, candles[_highIndex].OpenTime),
            Search.Low => new PendingSwing(
                SwingType.Low, _lowPrice, _lowIndex, candles[_lowIndex].OpenTime),
            _ => null,
        };

        /// <summary>
        /// The threshold, when <paramref name="reversal"/> has met it. A missing or
        /// non-positive threshold confirms nothing, never everything.
        /// </summary>
        private static decimal? Cleared(decimal reversal, decimal? required) =>
            required is { } distance && distance > 0m && reversal >= distance ? distance : null;

        private SwingPoint? AdvanceHigh(
            IReadOnlyList<Candle> candles,
            Candle bar,
            int index,
            IReversalThreshold threshold)
        {
            if (bar.High > _highPrice)
            {
                _highPrice = bar.High;
                _highIndex = index;

                // A bar that sets an extreme never also confirms it: within one bar there is
                // no telling whether the low came before the high or after, and assuming it
                // came after would let one wide bar manufacture a pivot out of nothing. The
                // reversal is therefore measured only over bars strictly after this one.
                return null;
            }

            return Cleared(_highPrice - bar.Low, threshold.For(_highIndex, _highPrice))
                is { } distance
                ? ConfirmHigh(candles, bar, index, distance)
                : null;
        }

        private SwingPoint? AdvanceLow(
            IReadOnlyList<Candle> candles,
            Candle bar,
            int index,
            IReversalThreshold threshold)
        {
            if (bar.Low < _lowPrice)
            {
                _lowPrice = bar.Low;
                _lowIndex = index;

                return null;
            }

            return Cleared(bar.High - _lowPrice, threshold.For(_lowIndex, _lowPrice))
                is { } distance
                ? ConfirmLow(candles, bar, index, distance)
                : null;
        }

        /// <summary>
        /// Before a direction exists, both candidates are tracked and the first to clear its
        /// threshold settles it. No heuristic picks the initial direction; the market does.
        /// </summary>
        private SwingPoint? AdvanceUnknown(
            IReadOnlyList<Candle> candles,
            Candle bar,
            int index,
            IReversalThreshold threshold)
        {
            var extendedHigh = bar.High > _highPrice;
            var extendedLow = bar.Low < _lowPrice;

            if (extendedHigh)
            {
                _highPrice = bar.High;
                _highIndex = index;
            }

            if (extendedLow)
            {
                _lowPrice = bar.Low;
                _lowIndex = index;
            }

            var high = extendedHigh
                ? null
                : Cleared(_highPrice - bar.Low, threshold.For(_highIndex, _highPrice));

            var low = extendedLow
                ? null
                : Cleared(bar.High - _lowPrice, threshold.For(_lowIndex, _lowPrice));

            if (high is { } highDistance && (low is null || PrefersHigh(bar)))
            {
                return ConfirmHigh(candles, bar, index, highDistance);
            }

            return low is { } lowDistance ? ConfirmLow(candles, bar, index, lowDistance) : null;
        }

        /// <summary>
        /// Both candidates cleared their threshold on the same bar, which takes a bar wide
        /// enough to span both and so can only happen before a direction exists.
        /// </summary>
        /// <remarks>
        /// They are necessarily anchored to the same bar when this happens. Once the low has
        /// extended past its own anchor, confirming the high needs price below that new low —
        /// which would have extended the low instead, and an extending bar confirms nothing.
        /// So the tiebreak is just the larger reversal, with an exact tie going to the high to
        /// keep the result deterministic.
        /// </remarks>
        private bool PrefersHigh(Candle bar) =>
            _highPrice - bar.Low >= bar.High - _lowPrice;

        private SwingPoint ConfirmHigh(
            IReadOnlyList<Candle> candles,
            Candle bar,
            int index,
            decimal distance)
        {
            var point = new SwingPoint(
                SwingType.High,
                _highPrice,
                _highIndex,
                candles[_highIndex].OpenTime,
                index,
                bar.Low,
                bar.OpenTime,
                distance,
                _highPrice - bar.Low);

            // The confirming bar's low is the lowest since the pivot — anything lower would
            // have breached the threshold earlier and confirmed there instead — so it is
            // exactly the next candidate, not an approximation of it.
            _lowPrice = bar.Low;
            _lowIndex = index;
            _search = Search.Low;

            return point;
        }

        private SwingPoint ConfirmLow(
            IReadOnlyList<Candle> candles,
            Candle bar,
            int index,
            decimal distance)
        {
            var point = new SwingPoint(
                SwingType.Low,
                _lowPrice,
                _lowIndex,
                candles[_lowIndex].OpenTime,
                index,
                bar.High,
                bar.OpenTime,
                distance,
                bar.High - _lowPrice);

            _highPrice = bar.High;
            _highIndex = index;
            _search = Search.High;

            return point;
        }
    }
}
