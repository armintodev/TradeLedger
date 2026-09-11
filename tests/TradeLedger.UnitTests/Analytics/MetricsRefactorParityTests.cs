using TradeLedger.Core.Analytics;
using TradeLedger.Core.Domain;

namespace TradeLedger.UnitTests.Analytics;

public class MetricsRefactorParityTests
{
    private static readonly DateTimeOffset Origin =
        new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SummaryMatchesLegacyOnTheHandBuiltFixture()
    {
        AssertSummaryParity(HandBuiltFixture());
    }

    [Fact]
    public void SummaryMatchesLegacyOnAnEmptyJournal()
    {
        AssertSummaryParity([]);
    }

    [Fact]
    public void SummaryMatchesLegacyWhenEveryTradeIsStillOpen()
    {
        AssertSummaryParity(
        [
            NewTrade(TradeOutcome.Open, 0m),
            NewTrade(TradeOutcome.Open, 0m),
        ]);
    }

    [Fact]
    public void SummaryMatchesLegacyWhenNothingEverLost()
    {
        AssertSummaryParity(
        [
            NewTrade(TradeOutcome.Win, 100m),
            NewTrade(TradeOutcome.Win, 250m),
            NewTrade(TradeOutcome.Breakeven, 0m),
        ]);
    }

    [Fact]
    public void SummaryMatchesLegacyWhenNothingEverWon()
    {
        AssertSummaryParity(
        [
            NewTrade(TradeOutcome.Loss, -100m),
            NewTrade(TradeOutcome.Loss, -50m),
        ]);
    }

    [Fact]
    public void SummaryMatchesLegacyWhenEveryOptionalFieldIsNull()
    {
        AssertSummaryParity(
        [
            NewTrade(TradeOutcome.Win, 10m, achievedR: null, plannedR: null, duration: null),
            NewTrade(TradeOutcome.Loss, -10m, achievedR: null, plannedR: null, duration: null),
        ]);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void SummaryMatchesLegacyAcrossManyGeneratedJournals(int seed)
    {
        var random = new Random(seed);

        for (var iteration = 0; iteration < 40; iteration++)
        {
            AssertSummaryParity(RandomJournal(random, random.Next(0, 60)));
        }
    }

    [Fact]
    public void EquityCurveMatchesLegacyOnARealisticCurve()
    {
        AssertEquityParity(
        [
            Point(0, 1000m),
            Point(1, 1120m),
            Point(2, 980m),
            Point(3, 1340m),
            Point(4, 700m),
            Point(5, 1500m),
        ]);
    }

    [Fact]
    public void EquityCurveMatchesLegacyWhenThereAreNoPoints()
    {
        AssertEquityParity([]);
    }

    [Fact]
    public void EquityCurveMatchesLegacyWhenItOnlyEverRises()
    {
        AssertEquityParity([Point(0, 100m), Point(1, 200m), Point(2, 300m)]);
    }

    [Fact]
    public void EquityCurveMatchesLegacyWhenItOpensAtZero()
    {
        AssertEquityParity([Point(0, 0m), Point(1, 0m), Point(2, 50m), Point(3, 25m)]);
    }

    [Fact]
    public void EquityCurveMatchesLegacyWhenEquityGoesNegative()
    {
        AssertEquityParity([Point(0, 500m), Point(1, -120m), Point(2, 60m)]);
    }

    [Theory]
    [InlineData(11)]
    [InlineData(22)]
    [InlineData(33)]
    public void EquityCurveMatchesLegacyAcrossManyGeneratedCurves(int seed)
    {
        var random = new Random(seed);

        for (var iteration = 0; iteration < 40; iteration++)
        {
            var count = random.Next(0, 80);
            var points = new List<EquityPoint>(count);

            for (var i = 0; i < count; i++)
            {
                points.Add(Point(i, Money(random, -2000, 5000)));
            }

            AssertEquityParity(points);
        }
    }

    private static void AssertSummaryParity(List<TradeMetricInput> trades)
    {
        var legacy = LegacyAnalyticsReference.Summary(trades);
        var extracted = PerformanceMetrics.Compute(trades);

        Assert.Equal(legacy, extracted);
    }

    private static void AssertEquityParity(List<EquityPoint> points)
    {
        var legacy = LegacyAnalyticsReference.Equity(points);
        var extracted = EquityMath.Analyse(points);

        Assert.Equal(legacy.Points, extracted.Points);
        Assert.Equal(legacy.StartEquity, extracted.StartEquity);
        Assert.Equal(legacy.CurrentEquity, extracted.CurrentEquity);
        Assert.Equal(legacy.PeakEquity, extracted.PeakEquity);
        Assert.Equal(legacy.MaxDrawdown, extracted.MaxDrawdown);
        Assert.Equal(legacy.MaxDrawdownPercent, extracted.MaxDrawdownPercent);
        Assert.Equal(legacy.MaxDrawdownAt, extracted.MaxDrawdownAt);
        Assert.Equal(legacy.CurrentDrawdown, extracted.CurrentDrawdown);
        Assert.Equal(legacy.CurrentDrawdownPercent, extracted.CurrentDrawdownPercent);
    }

    private static List<TradeMetricInput> HandBuiltFixture()
    {
        var trades = new List<TradeMetricInput>();

        for (var i = 0; i < 30; i++)
        {
            var outcome = (i % 7) switch
            {
                0 or 1 or 2 => TradeOutcome.Win,
                3 or 4 => TradeOutcome.Loss,
                5 => TradeOutcome.Breakeven,
                _ => TradeOutcome.Open,
            };

            var net = outcome switch
            {
                TradeOutcome.Win => 40m + i * 3.25m,
                TradeOutcome.Loss => -(20m + i * 1.75m),
                _ => 0m,
            };

            trades.Add(NewTrade(
                outcome,
                net,
                index: i,
                achievedR: i % 4 == 0 ? null : decimal.Round(net / 50m, 6),
                plannedR: i % 5 == 0 ? null : 2m,
                duration: i % 6 == 0 ? null : TimeSpan.FromMinutes(45 + i * 11),
                isPlanned: i % 3 != 0));
        }

        return trades;
    }

    private static List<TradeMetricInput> RandomJournal(Random random, int count)
    {
        var trades = new List<TradeMetricInput>(count);

        for (var i = 0; i < count; i++)
        {
            var outcome = (TradeOutcome)random.Next(0, 4);

            var net = outcome switch
            {
                TradeOutcome.Win => Money(random, 1, 900),
                TradeOutcome.Loss => -Money(random, 1, 700),
                _ => 0m,
            };

            trades.Add(NewTrade(
                outcome,
                net,
                index: random.Next(0, 500),
                achievedR: random.Next(0, 4) == 0 ? null : Money(random, -400, 600) / 100m,
                plannedR: random.Next(0, 4) == 0 ? null : Money(random, 0, 500) / 100m,
                duration: random.Next(0, 4) == 0
                    ? null
                    : TimeSpan.FromSeconds(random.Next(60, 900000)),
                isPlanned: random.Next(0, 2) == 0,
                grossOffset: Money(random, -50, 50),
                fees: Money(random, 0, 40),
                funding: Money(random, -25, 25)));
        }

        return trades;
    }

    private static TradeMetricInput NewTrade(
        TradeOutcome outcome,
        decimal net,
        int index = 0,
        decimal? achievedR = 1.5m,
        decimal? plannedR = 2m,
        TimeSpan? duration = null,
        bool isPlanned = true,
        decimal grossOffset = 0m,
        decimal fees = 0m,
        decimal funding = 0m) => new(
        OpenedAt: Origin.AddHours(index),
        ClosedAt: outcome == TradeOutcome.Open ? null : Origin.AddHours(index).AddHours(3),
        GrossProfitLoss: net + grossOffset,
        Fees: fees,
        Funding: funding,
        NetProfitLoss: net,
        Outcome: outcome,
        AchievedReturnR: achievedR,
        PlannedReturnR: plannedR,
        IsPlanned: isPlanned,
        Duration: duration);

    private static EquityPoint Point(int hour, decimal equity) =>
        new(Origin.AddHours(hour), equity);

    private static decimal Money(Random random, int min, int max) =>
        decimal.Round((decimal)(random.NextDouble() * (max - min) + min), 8);
}
