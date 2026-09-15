using TradeLedger.Core.Domain.MarketData;
using TradeLedger.Core.MarketStructure;
using TradeLedger.Core.MarketStructure.Thresholds;

namespace TradeLedger.UnitTests.MarketStructure;

public class SwingDetectorTests
{
    /// <summary>
    /// The worked example the detector was specified from: price runs 100 to 110, turns, and
    /// the high is confirmed on the bar that has fallen the threshold away from it.
    /// </summary>
    [Fact]
    public void TheWalkthroughConfirmsTheHighOnTheBarThatFellTwoAwayFromIt()
    {
        var candles = Flat(100m, 102m, 105m, 108m, 110m, 109m, 107m, 105m);

        var series = SwingDetector.Detect(candles, new FixedReversalThreshold(2m));

        Assert.Equal(2, series.Points.Count);

        // The opening low is confirmed as soon as price rises the threshold away from it. It is
        // an artifact of where the range starts rather than a turn the market made, which is
        // exactly what sitting at FirstDetectableIndex marks it as.
        var opening = series.Points[0];
        Assert.Equal(SwingType.Low, opening.Type);
        Assert.Equal(100m, opening.Price);
        Assert.Equal(series.FirstDetectableIndex, opening.CandleIndex);

        var high = series.Points[1];
        Assert.Equal(SwingType.High, high.Type);
        Assert.Equal(110m, high.Price);
        Assert.Equal(4, high.CandleIndex);
        Assert.Equal(candles[4].OpenTime, high.Time);
        Assert.Equal(6, high.ConfirmationIndex);
        Assert.Equal(107m, high.ConfirmationPrice);
        Assert.Equal(candles[6].OpenTime, high.ConfirmationTime);
        Assert.Equal(2m, high.Threshold);
        Assert.Equal(3m, high.Reversal);
        Assert.Equal(2, high.BarsToConfirm);

        // 109 was not enough, so the pivot was still a guess one bar earlier.
        Assert.Single(series.KnownAt(5));
        Assert.Equal(2, series.KnownAt(6).Count);

        Assert.Equal(
            new PendingSwing(SwingType.Low, 105m, 7, candles[7].OpenTime),
            series.Pending);
    }

    [Fact]
    public void TheCandidateKeepsTheHighestHighSinceTheLastPivot()
    {
        var candles = Flat(100m, 105m, 107m, 108m, 109m, 108m);

        var series = SwingDetector.Detect(candles, new FixedReversalThreshold(2m));

        // Nothing has pulled back two from 109 yet, so it is still only a candidate.
        Assert.Equal(new PendingSwing(SwingType.High, 109m, 4, candles[4].OpenTime), series.Pending);
        Assert.DoesNotContain(series.Points, p => p.Type == SwingType.High);
    }

    [Fact]
    public void TheCandidateKeepsTheLowestLowSinceTheLastPivot()
    {
        var candles = Flat(100m, 95m, 103m, 101m, 99m, 100m);

        var series = SwingDetector.Detect(candles, new FixedReversalThreshold(2m));

        Assert.Equal(new PendingSwing(SwingType.Low, 99m, 4, candles[4].OpenTime), series.Pending);
    }

    /// <summary>
    /// Bar 3 prints both a new high and a low well past the trigger level. Which came first
    /// inside the bar is unknowable, so the reversal is not counted and the bar only extends
    /// the candidate.
    /// </summary>
    [Fact]
    public void ABarThatSetsANewExtremeCannotAlsoConfirmIt()
    {
        List<Candle> candles =
        [
            Bar(0, low: 100m, high: 100m),
            Bar(1, low: 104m, high: 104m),
            Bar(2, low: 105m, high: 110m),
            Bar(3, low: 100m, high: 112m),
            Bar(4, low: 111m, high: 111.5m),
        ];

        var series = SwingDetector.Detect(candles, new FixedReversalThreshold(2m));

        var opening = Assert.Single(series.Points);
        Assert.Equal(SwingType.Low, opening.Type);

        Assert.Equal(
            new PendingSwing(SwingType.High, 112m, 3, candles[3].OpenTime),
            series.Pending);
    }

    [Fact]
    public void AFallingOpenYieldsAHighFirstAndARisingOpenALow()
    {
        var threshold = new FixedReversalThreshold(2m);

        var falling = SwingDetector.Detect(Flat(100m, 99m, 98m, 97m), threshold);
        var rising = SwingDetector.Detect(Flat(100m, 101m, 102m, 103m), threshold);

        Assert.Equal(SwingType.High, falling.Points[0].Type);
        Assert.Equal(100m, falling.Points[0].Price);

        Assert.Equal(SwingType.Low, rising.Points[0].Type);
        Assert.Equal(100m, rising.Points[0].Price);
    }

    /// <summary>
    /// A bar wide enough to clear both candidates at once, which only the undecided state can
    /// produce. The reversals are equal here, so the tiebreak falls through to the high.
    /// </summary>
    [Fact]
    public void ABarClearingBothCandidatesResolvesDeterministically()
    {
        List<Candle> candles =
        [
            Bar(0, low: 100m, high: 100m),
            Bar(1, low: 95m, high: 105m),
            Bar(2, low: 98m, high: 102m),
        ];

        var series = SwingDetector.Detect(candles, new FixedReversalThreshold(2m));

        var point = Assert.Single(series.Points);

        Assert.Equal(SwingType.High, point.Type);
        Assert.Equal(105m, point.Price);
        Assert.Equal(1, point.CandleIndex);
        Assert.Equal(2, point.ConfirmationIndex);
    }

    [Fact]
    public void TypesAlternateAndBothIndexColumnsAdvance()
    {
        var series = SwingDetector.Detect(Noisy(600), new FixedReversalThreshold(3m));

        Assert.True(series.Points.Count > 4, $"expected several turns, found {series.Points.Count}");

        for (var i = 1; i < series.Points.Count; i++)
        {
            var previous = series.Points[i - 1];
            var current = series.Points[i];

            Assert.NotEqual(previous.Type, current.Type);
            Assert.True(current.CandleIndex > previous.CandleIndex);
            Assert.True(current.ConfirmationIndex > previous.ConfirmationIndex);
        }
    }

    [Fact]
    public void EveryPivotIsConfirmedLaterThanItPrintedAndByAtLeastItsThreshold()
    {
        var series = SwingDetector.Detect(Noisy(600), new FixedReversalThreshold(5m));

        Assert.NotEmpty(series.Points);

        Assert.All(series.Points, point =>
        {
            Assert.True(point.ConfirmationIndex > point.CandleIndex);
            Assert.True(point.BarsToConfirm >= 1);
            Assert.True(point.Reversal >= point.Threshold);
        });
    }

    /// <summary>
    /// The guard against reading the future: at any bar, the series may only admit the pivots
    /// that had already been confirmed by then.
    /// </summary>
    [Fact]
    public void KnownAtNeverReturnsAPivotThatHadNotBeenConfirmedYet()
    {
        var candles = Noisy(400);
        var series = SwingDetector.Detect(candles, new FixedReversalThreshold(6m));

        Assert.NotEmpty(series.Points);

        for (var bar = -1; bar <= candles.Count; bar++)
        {
            var known = series.KnownAt(bar);

            Assert.All(known, point => Assert.True(point.ConfirmationIndex <= bar));
            Assert.Equal(series.Points.Count(p => p.ConfirmationIndex <= bar), known.Count);
        }
    }

    [Fact]
    public void WithAnAtrThresholdNothingIsAnchoredBeforeTheAtrIsWarm()
    {
        var candles = Noisy(300);

        var series = SwingDetector.Detect(
            candles, AtrReversalThreshold.From(candles, period: 14, multiplier: 1.5m));

        Assert.Equal(14, series.FirstDetectableIndex);
        Assert.NotEmpty(series.Points);
        Assert.All(series.Points, point => Assert.True(point.CandleIndex >= 14));
    }

    [Fact]
    public void ARangeTooShortForTheThresholdIsNeverDetectable()
    {
        var candles = Flat(100m, 101m, 102m);

        var series = SwingDetector.Detect(
            candles, AtrReversalThreshold.From(candles, period: 14, multiplier: 1m));

        Assert.Equal(candles.Count, series.FirstDetectableIndex);
        Assert.Empty(series.Points);
        Assert.Null(series.Pending);
    }

    [Fact]
    public void DegenerateRangesProduceNothingRatherThanThrowing()
    {
        var threshold = new FixedReversalThreshold(1m);

        var empty = SwingDetector.Detect([], threshold);
        Assert.Empty(empty.Points);
        Assert.Null(empty.Pending);
        Assert.Equal(0, empty.FirstDetectableIndex);

        var single = SwingDetector.Detect(Flat(100m), threshold);
        Assert.Empty(single.Points);
        Assert.Null(single.Pending);

        var unmoving = SwingDetector.Detect(Flat(100m, 100m, 100m, 100m), threshold);
        Assert.Empty(unmoving.Points);
        Assert.Null(unmoving.Pending);
    }

    /// <summary>
    /// Not a claimed theorem about the state machine, but the behaviour tuning depends on: a
    /// wider threshold has to ignore the smaller legs.
    /// </summary>
    [Fact]
    public void AWiderThresholdFindsFewerPivots()
    {
        var candles = Noisy(500);

        var tight = SwingDetector.Detect(candles, new FixedReversalThreshold(2m)).Points.Count;
        var loose = SwingDetector.Detect(candles, new FixedReversalThreshold(8m)).Points.Count;
        var widest = SwingDetector.Detect(candles, new FixedReversalThreshold(30m)).Points.Count;

        Assert.True(tight > loose, $"2 found {tight}, 8 found {loose}");
        Assert.True(loose > widest, $"8 found {loose}, 30 found {widest}");
    }

    /// <summary>
    /// A triangular wave with noise on top: twenty bars up, twenty down, so there is always
    /// another leg coming, and the level is a function of the bar index rather than a random
    /// walk so prices cannot drift anywhere near zero.
    /// </summary>
    private static List<Candle> Noisy(int count)
    {
        var random = new Random(20260916);
        var candles = new List<Candle>(count);

        for (var i = 0; i < count; i++)
        {
            var phase = i % 40;
            var ramp = phase < 20 ? phase : 40 - phase;

            var mid = 1000m
                + (ramp * 8m)
                + Math.Round((decimal)((random.NextDouble() - 0.5) * 10), 6);

            var span = Math.Round((decimal)(random.NextDouble() * 6), 6) + 0.5m;

            candles.Add(Bar(i, low: mid - span, high: mid + span));
        }

        return candles;
    }

    private static List<Candle> Flat(params decimal[] prices)
    {
        var candles = new List<Candle>(prices.Length);

        for (var i = 0; i < prices.Length; i++)
        {
            candles.Add(Bar(i, low: prices[i], high: prices[i]));
        }

        return candles;
    }

    private static Candle Bar(int index, decimal low, decimal high) =>
        Candle.Of(
            CandleSource.BinanceFutures,
            "BTCUSDT",
            CandleInterval.FifteenMinutes,
            DateTimeOffset.UnixEpoch.AddMinutes(15 * index),
            low,
            high,
            low,
            high,
            volume: 100m);
}
