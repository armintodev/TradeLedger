using TradeLedger.Core.Domain.MarketData;

namespace TradeLedger.Core.Backtesting.Indicators;

/// <summary>
/// Re-expresses a higher-timeframe indicator on the run's own bar axis, so every
/// consumer downstream keeps indexing by base bar. See SPEC.md section 6.8.2.
/// </summary>
public static class IndicatorProjection
{
    /// <summary>
    /// Maps <paramref name="htf"/> onto <paramref name="baseBars"/>.
    /// </summary>
    /// <remarks>
    /// At base bar <c>i</c> the visible value is the one from the last higher-timeframe
    /// bucket whose close is at or before bar <c>i</c>'s own close. A bucket therefore
    /// becomes visible on the final base bar that composes it — the moment it closes,
    /// and not before. That is what keeps the read free of lookahead without paying an
    /// extra bucket of lag, and it is the assertion the projection test pins.
    /// </remarks>
    public static IndicatorSeries ProjectOntoBase(
        IndicatorSeries htf,
        IReadOnlyList<Candle> htfBars,
        IReadOnlyList<Candle> baseBars)
    {
        if (htf.Length != htfBars.Count)
        {
            throw new ArgumentException(
                $"The series holds {htf.Length} values but {htfBars.Count} bars were supplied.",
                nameof(htf));
        }

        var names = htf.Outputs.ToArray();
        var projected = new decimal?[names.Length][];

        for (var output = 0; output < names.Length; output++)
        {
            projected[output] = new decimal?[baseBars.Count];
        }

        // The last bucket known to have closed, and the next one to consider. Both only
        // ever move forward, so this is a single walk rather than a search per bar.
        var closed = -1;
        var next = 0;

        for (var i = 0; i < baseBars.Count; i++)
        {
            var knownBy = baseBars[i].CloseTime;

            while (next < htfBars.Count && htfBars[next].CloseTime <= knownBy)
            {
                closed = next;
                next++;
            }

            if (closed < 0)
            {
                continue;
            }

            for (var output = 0; output < names.Length; output++)
            {
                projected[output][i] = htf.At(closed, names[output]);
            }
        }

        var outputs = new Dictionary<string, decimal?[]>(names.Length, StringComparer.OrdinalIgnoreCase);

        for (var output = 0; output < names.Length; output++)
        {
            outputs[names[output]] = projected[output];
        }

        return new IndicatorSeries(baseBars.Count, outputs);
    }
}
