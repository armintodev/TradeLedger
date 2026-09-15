using TradeLedger.Core.Domain.MarketData;

namespace TradeLedger.Core.MarketData;

/// <summary>
/// Builds higher-timeframe bars out of a run's own candle series, so a rule may read
/// an indicator on a timeframe above the one it trades. See SPEC.md section 6.8.1.
/// </summary>
/// <remarks>
/// The output never reaches the database. Aggregated bars carry no identity and exist
/// only to feed indicator computation; handing one to a tracked <c>DbContext</c> would
/// try to INSERT it against the unique (Source, Symbol, Interval, OpenTime) index.
/// </remarks>
public static class CandleAggregator
{
    /// <summary>
    /// Aggregates <paramref name="source"/> up to <paramref name="target"/>.
    /// </summary>
    /// <param name="source">Ascending bars, all of one interval, as the repository returns them.</param>
    /// <param name="target">The interval to build. Must be a whole multiple of the source's.</param>
    /// <param name="warnings">Collects one note if any interior bucket was short of bars.</param>
    public static IReadOnlyList<Candle> Aggregate(
        IReadOnlyList<Candle> source,
        CandleInterval target,
        ICollection<string>? warnings = null)
    {
        if (source.Count == 0)
        {
            return [];
        }

        var baseInterval = source[0].Interval;

        if (target == baseInterval)
        {
            return source;
        }

        var ratio = target.RatioTo(baseInterval);
        var targetDuration = target.Duration();
        var firstOpen = source[0].OpenTime;
        var lastClose = source[^1].CloseTime;

        var result = new List<Candle>(source.Count / ratio + 1);
        var thinBuckets = 0;
        var index = 0;

        while (index < source.Count)
        {
            var bucketOpen = target.AlignFloor(source[index].OpenTime);
            var bucketClose = bucketOpen + targetDuration;

            var open = source[index].Open;
            var high = source[index].High;
            var low = source[index].Low;
            var close = source[index].Close;
            var volume = 0m;
            var quoteVolume = 0m;
            var quoteVolumeKnown = true;
            var tradeCount = 0;
            var tradeCountKnown = true;
            var members = 0;

            while (index < source.Count && source[index].OpenTime < bucketClose)
            {
                var bar = source[index];

                if (bar.High > high)
                {
                    high = bar.High;
                }

                if (bar.Low < low)
                {
                    low = bar.Low;
                }

                close = bar.Close;
                volume += bar.Volume;

                // Summing a nullable with null treated as zero would report a confident
                // number where the truth is "unknown". One missing member poisons the total.
                if (bar.QuoteVolume is { } quote)
                {
                    quoteVolume += quote;
                }
                else
                {
                    quoteVolumeKnown = false;
                }

                if (bar.TradeCount is { } trades)
                {
                    tradeCount += trades;
                }
                else
                {
                    tradeCountKnown = false;
                }

                members++;
                index++;
            }

            // A bucket that starts before the loaded range opens at a mid-bucket price and
            // would seed every Wilder-smoothed indicator off a bar that never existed. One
            // that ends after it has not closed yet, so the projection must never see it.
            if (bucketOpen < firstOpen || bucketClose > lastClose)
            {
                continue;
            }

            // An interior bucket short of bars is aggregated anyway. Dropping it would leave
            // the series contiguous in index but not in time, and every indexed indicator
            // would silently smooth across the hole with no way to detect it.
            if (members < ratio)
            {
                thinBuckets++;
            }

            result.Add(Candle.Of(
                source[0].Source,
                source[0].Symbol,
                target,
                bucketOpen,
                open,
                high,
                low,
                close,
                volume,
                quoteVolumeKnown ? quoteVolume : null,
                tradeCountKnown ? tradeCount : null));
        }

        if (thinBuckets > 0)
        {
            warnings?.Add(
                $"{thinBuckets} of {result.Count} {target} bars were built from an incomplete " +
                "set of source candles. Their values are approximate; backfill the gaps for a " +
                "trustworthy result.");
        }

        return result;
    }
}
