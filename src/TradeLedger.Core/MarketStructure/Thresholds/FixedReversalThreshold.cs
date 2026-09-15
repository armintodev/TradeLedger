namespace TradeLedger.Core.MarketStructure.Thresholds;

/// <summary>
/// A constant reversal distance in quote currency: no volatility estimate, no dependence on
/// the price level. Being entirely predictable is what makes it the threshold the detector's
/// own tests are written against.
/// </summary>
public sealed class FixedReversalThreshold : IReversalThreshold
{
    private readonly decimal _distance;

    public FixedReversalThreshold(decimal distance)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(distance);

        _distance = distance;
    }

    public int FirstAvailableIndex => 0;

    public decimal? For(int anchorIndex, decimal anchorPrice) => _distance;
}
