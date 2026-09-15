using TradeLedger.Core.Backtesting.Indicators;

namespace TradeLedger.UnitTests.Backtesting;

public class RollingExtremeTests
{
    [Fact]
    public void TheWindowIncludesTheCurrentBar()
    {
        decimal[] values = [1m, 5m, 3m, 2m, 9m];

        var highest = IndicatorMath.Highest(values, 3);

        Assert.Equal(5m, highest[2]);
        Assert.Equal(5m, highest[3]);
        Assert.Equal(9m, highest[4]);
    }

    [Fact]
    public void NothingIsReportedUntilTheWindowIsFull()
    {
        decimal[] values = [4m, 2m, 7m, 1m];

        var highest = IndicatorMath.Highest(values, 3);
        var lowest = IndicatorMath.Lowest(values, 3);

        Assert.Null(highest[0]);
        Assert.Null(highest[1]);
        Assert.NotNull(highest[2]);
        Assert.Equal(2m, lowest[2]);
        Assert.Equal(1m, lowest[3]);
    }

    [Fact]
    public void ASeriesShorterThanThePeriodIsAllNull()
    {
        decimal[] values = [4m, 2m];

        Assert.All(IndicatorMath.Highest(values, 5), v => Assert.Null(v));
        Assert.All(IndicatorMath.Lowest(values, 5), v => Assert.Null(v));
    }

    /// <summary>
    /// The deque is the only part of these that is not obviously correct by reading, so it is
    /// checked against the naive window it replaces.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(7)]
    [InlineData(31)]
    public void TheDequeAgreesWithANaiveWindow(int period)
    {
        var random = new Random(20260915);
        var values = new decimal[400];

        for (var i = 0; i < values.Length; i++)
        {
            values[i] = Math.Round((decimal)(random.NextDouble() * 1000), 8);
        }

        var highest = IndicatorMath.Highest(values, period);
        var lowest = IndicatorMath.Lowest(values, period);

        for (var i = period - 1; i < values.Length; i++)
        {
            var window = values.Skip(i - period + 1).Take(period).ToArray();

            Assert.Equal(window.Max(), highest[i]);
            Assert.Equal(window.Min(), lowest[i]);
        }
    }

    [Fact]
    public void TheFactoryReadsHighsForHighestAndLowsForLowest()
    {
        Assert.Equal(PriceSource.High, IndicatorFactory.DefaultSourceFor("Highest"));
        Assert.Equal(PriceSource.Low, IndicatorFactory.DefaultSourceFor("Lowest"));
        Assert.Equal(PriceSource.Close, IndicatorFactory.DefaultSourceFor("Ema"));
    }

    [Fact]
    public void BothAreWarmAfterExactlyOnePeriod()
    {
        Assert.Equal(20, IndicatorFactory.WarmupBars("Highest", 20));
        Assert.Equal(20, IndicatorFactory.WarmupBars("Lowest", 20));
    }
}
