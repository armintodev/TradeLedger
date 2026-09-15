using TradeLedger.Core.MarketStructure.Thresholds;

namespace TradeLedger.UnitTests.MarketStructure;

public class AtrReversalThresholdTests
{
    /// <summary>
    /// The behaviour the "frozen at the candidate's bar" decision buys: the answer depends only
    /// on the anchor it is asked about, never on how far the detector has since walked.
    /// </summary>
    [Fact]
    public void TheThresholdIsReadAtTheAnchorNotAtTheBarBeingEvaluated()
    {
        decimal?[] atr = [null, null, 1m, 4m, 9m];

        var threshold = new AtrReversalThreshold(atr, 1.5m);

        Assert.Equal(2, threshold.FirstAvailableIndex);
        Assert.Equal(1.5m, threshold.For(2, anchorPrice: 100m));
        Assert.Equal(6m, threshold.For(3, anchorPrice: 100m));
        Assert.Equal(13.5m, threshold.For(4, anchorPrice: 100m));
    }

    [Fact]
    public void TheAnchorPriceIsIgnoredBecauseAtrIsAlreadyAnAbsoluteDistance()
    {
        var threshold = new AtrReversalThreshold([null, 2m], 3m);

        Assert.Equal(threshold.For(1, anchorPrice: 1m), threshold.For(1, anchorPrice: 90_000m));
    }

    [Fact]
    public void AnUnwarmOrOutOfRangeAnchorHasNoThreshold()
    {
        var threshold = new AtrReversalThreshold([null, 2m], 2m);

        Assert.Null(threshold.For(0, anchorPrice: 100m));
        Assert.Null(threshold.For(-1, anchorPrice: 100m));
        Assert.Null(threshold.For(5, anchorPrice: 100m));
    }

    [Fact]
    public void AnAllNullSeriesNeverBecomesAvailable()
    {
        var threshold = new AtrReversalThreshold([null, null, null], 1m);

        Assert.Equal(3, threshold.FirstAvailableIndex);
    }

    /// <summary>
    /// A zero or negative multiplier would make every bar clear every threshold, turning the
    /// detector into a pivot per bar. It is rejected rather than tolerated.
    /// </summary>
    [Fact]
    public void TheMultiplierMustBePositive()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AtrReversalThreshold([1m], 0m));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AtrReversalThreshold([1m], -1m));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FixedReversalThreshold(0m));
    }
}
