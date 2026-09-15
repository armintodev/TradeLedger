using TradeLedger.Core.Domain.MarketData;

namespace TradeLedger.Core.Backtesting;

/// <summary>
/// The market-cycle reading a rule run records against every position it opens.
///
/// It is deliberately independent of the strategy. "Which cycle did this open in" has to
/// be answerable for a run that declares no ADX at all — that is precisely the run where
/// the answer is worth having — so the engine always measures it. It is equally
/// deliberately invisible to the rules: the series never enters the indicator dictionary,
/// so no condition can read it and it takes no part in deciding when indicators are warm.
/// A hidden indicator that did either would change how every existing strategy behaves.
/// </summary>
public static class CycleReference
{
    /// <summary>Wilder's own period, and what the chart in front of the trader shows.</summary>
    public const int Period = 14;

    /// <summary>The indicator the reading is taken from. ADX derives from the whole bar.</summary>
    public const string Indicator = "Adx";

    /// <summary>
    /// A cycle is a higher-timeframe property — a fifteen-minute ADX says nothing about where
    /// the wave is. So the reading is taken on four-hour bars wherever they can be built from
    /// the run's own candles, and on the run's own interval where they cannot: 6h, 1d and 1w
    /// are not whole divisors of four hours, and aggregating to a coarser grid than the run
    /// trades on would be the more surprising answer anyway.
    /// </summary>
    public static CandleInterval IntervalFor(CandleInterval runInterval) =>
        runInterval.DividesInto(CandleInterval.FourHours)
            ? CandleInterval.FourHours
            : runInterval;
}
