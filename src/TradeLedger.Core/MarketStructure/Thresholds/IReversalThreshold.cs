namespace TradeLedger.Core.MarketStructure.Thresholds;

/// <summary>
/// How far price must reverse before a candidate extreme is accepted as a swing point. This is
/// the one thing the detector cannot work out for itself, so it asks.
/// </summary>
/// <remarks>
/// The anchor is the candidate — its bar and its price — not the bar being evaluated. That is
/// what lets a single seam serve every family of threshold: an ATR threshold reads the index,
/// a percentage threshold reads the price, a fixed one reads neither. It also holds the
/// threshold still for the life of a candidate, so a pivot records the exact distance it had
/// to beat, and volatility expanding during the pullback cannot raise the bar retroactively.
/// </remarks>
public interface IReversalThreshold
{
    /// <summary>
    /// The first bar index at which <see cref="For"/> can return a value. The detector ignores
    /// bars before it, so a candidate is never anchored where its threshold could never be
    /// computed. Past the end of the range means the detector never starts.
    /// </summary>
    int FirstAvailableIndex { get; }

    /// <summary>
    /// The reversal needed to confirm a candidate sitting at <paramref name="anchorPrice"/> on
    /// bar <paramref name="anchorIndex"/>. Null or non-positive confirms nothing — never
    /// everything.
    /// </summary>
    decimal? For(int anchorIndex, decimal anchorPrice);
}
