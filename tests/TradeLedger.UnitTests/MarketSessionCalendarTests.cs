using TradeLedger.Core.Domain;

namespace TradeLedger.UnitTests;

public class MarketSessionCalendarTests
{
    [Theory]
    [InlineData(0, MarketSession.Tokyo)]
    [InlineData(3, MarketSession.Tokyo)]
    [InlineData(6, MarketSession.Tokyo)]
    [InlineData(9, MarketSession.London)]
    [InlineData(11, MarketSession.London)]
    [InlineData(16, MarketSession.NewYork)]
    [InlineData(20, MarketSession.NewYork)]
    [InlineData(21, MarketSession.None)]
    [InlineData(23, MarketSession.None)]
    public void EachHourLandsInTheExpectedSession(int hour, MarketSession expected)
    {
        Assert.Equal(expected, MarketSessionCalendar.At(At(hour)));
    }

    [Theory]
    [InlineData(7)]
    [InlineData(8)]
    public void TokyoAndLondonOverlapInTheEarlyMorning(int hour)
    {
        var session = MarketSessionCalendar.At(At(hour));

        Assert.Equal(MarketSession.Tokyo | MarketSession.London, session);
        Assert.True(MarketSessionCalendar.IsOverlap(session));
    }

    [Theory]
    [InlineData(12)]
    [InlineData(14)]
    [InlineData(15)]
    public void LondonAndNewYorkOverlapInTheAfternoon(int hour)
    {
        var session = MarketSessionCalendar.At(At(hour));

        Assert.Equal(MarketSession.London | MarketSession.NewYork, session);
        Assert.True(MarketSessionCalendar.IsOverlap(session));
    }

    [Fact]
    public void ASingleSessionIsNotAnOverlap()
    {
        Assert.False(MarketSessionCalendar.IsOverlap(MarketSession.London));
        Assert.False(MarketSessionCalendar.IsOverlap(MarketSession.None));
    }

    [Fact]
    public void TheSessionIsReadInUtcNotInTheOffsetOfTheInstant()
    {
        var sameInstantDifferentOffsets = new DateTimeOffset(2026, 1, 5, 14, 0, 0, TimeSpan.FromHours(9));

        Assert.Equal(TimeSpan.FromHours(5), sameInstantDifferentOffsets.ToUniversalTime().TimeOfDay);
        Assert.Equal(MarketSession.Tokyo, MarketSessionCalendar.At(sameInstantDifferentOffsets));
    }

    [Fact]
    public void SessionBoundariesAreHalfOpenSoNoHourBelongsToTwoRangesByAccident()
    {
        Assert.Equal(MarketSession.Tokyo, MarketSessionCalendar.At(At(0)));
        Assert.Equal(MarketSession.London, MarketSessionCalendar.At(At(9)));
        Assert.Equal(MarketSession.None, MarketSessionCalendar.At(At(21)));
    }

    [Theory]
    [InlineData(MarketSession.None, "Off hours")]
    [InlineData(MarketSession.Tokyo, "Tokyo")]
    [InlineData(MarketSession.NewYork, "New York")]
    [InlineData(MarketSession.London | MarketSession.NewYork, "London / New York overlap")]
    public void DescribeNamesTheSessionTheWayATraderWould(MarketSession session, string expected)
    {
        Assert.Equal(expected, MarketSessionCalendar.Describe(session));
    }

    [Fact]
    public void ATradeDerivesItsSessionFromTheInstantItOpened()
    {
        var trade = Trade.OpenManual(new NewManualTrade
        {
            AccountId = Guid.CreateVersion7(),
            Symbol = "BTCUSDT",
            Side = TradeSide.Long,
            EntryPrice = 100m,
            Quantity = 1m,
            OpenedAt = At(13),
        });

        Assert.Equal(MarketSession.London | MarketSession.NewYork, trade.MarketSession);
    }

    private static DateTimeOffset At(int hour) => new(2026, 1, 5, hour, 0, 0, TimeSpan.Zero);
}
