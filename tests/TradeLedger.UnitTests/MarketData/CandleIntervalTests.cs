using System.Globalization;
using TradeLedger.Core.Domain.MarketData;
using TradeLedger.Core.MarketData;

namespace TradeLedger.UnitTests.MarketData;

public class CandleIntervalTests
{
    [Fact]
    public void NothingShorterThanFifteenMinutesIsTradeable()
    {
        Assert.All(
            CandleIntervals.Tradeable,
            interval => Assert.True(interval.Duration() >= TimeSpan.FromMinutes(15)));
    }

    [Fact]
    public void OneMinuteExistsButIsNeverTradeable()
    {
        Assert.False(CandleInterval.OneMinute.IsTradeable());
        Assert.DoesNotContain(CandleInterval.OneMinute, CandleIntervals.Tradeable);
        Assert.Equal(CandleInterval.OneMinute, CandleIntervals.DrillDown);
    }

    [Fact]
    public void EveryDeclaredIntervalIsEitherTradeableOrTheDrillDownOne()
    {
        var declared = Enum.GetValues<CandleInterval>();

        Assert.Equal(
            declared.Length,
            CandleIntervals.Tradeable.Count + 1);
    }

    [Theory]
    [InlineData(CandleInterval.OneMinute, "1m")]
    [InlineData(CandleInterval.FifteenMinutes, "15m")]
    [InlineData(CandleInterval.ThirtyMinutes, "30m")]
    [InlineData(CandleInterval.OneHour, "1h")]
    [InlineData(CandleInterval.TwoHours, "2h")]
    [InlineData(CandleInterval.FourHours, "4h")]
    [InlineData(CandleInterval.SixHours, "6h")]
    [InlineData(CandleInterval.TwelveHours, "12h")]
    [InlineData(CandleInterval.OneDay, "1d")]
    [InlineData(CandleInterval.OneWeek, "1w")]
    public void MapsToTheBinanceIntervalCode(CandleInterval interval, string expected)
    {
        Assert.Equal(expected, interval.ToBinanceInterval());
    }

    [Theory]
    [InlineData(CandleInterval.FifteenMinutes, "2025-03-05T13:37:22Z", "2025-03-05T13:30:00Z")]
    [InlineData(CandleInterval.ThirtyMinutes, "2025-03-05T13:37:22Z", "2025-03-05T13:30:00Z")]
    [InlineData(CandleInterval.OneHour, "2025-03-05T13:37:22Z", "2025-03-05T13:00:00Z")]
    [InlineData(CandleInterval.FourHours, "2025-03-05T13:37:22Z", "2025-03-05T12:00:00Z")]
    [InlineData(CandleInterval.SixHours, "2025-03-05T13:37:22Z", "2025-03-05T12:00:00Z")]
    [InlineData(CandleInterval.TwelveHours, "2025-03-05T13:37:22Z", "2025-03-05T12:00:00Z")]
    [InlineData(CandleInterval.OneDay, "2025-03-05T13:37:22Z", "2025-03-05T00:00:00Z")]
    public void AlignsDownToTheIntervalBoundary(
        CandleInterval interval, string input, string expected)
    {
        var aligned = interval.AlignFloor(At(input));

        Assert.Equal(At(expected), aligned);
    }

    [Fact]
    public void WeeklyCandlesAlignToMondayNotToTheEpoch()
    {
        var wednesday = At("2025-01-01T13:37:00Z");

        var aligned = CandleInterval.OneWeek.AlignFloor(wednesday);

        Assert.Equal(At("2024-12-30T00:00:00Z"), aligned);
        Assert.Equal(DayOfWeek.Monday, aligned.DayOfWeek);
    }

    [Fact]
    public void AMondayIsAlreadyAlignedForWeeklyCandles()
    {
        var monday = At("2024-12-30T00:00:00Z");

        Assert.True(CandleInterval.OneWeek.IsAligned(monday));
        Assert.Equal(monday, CandleInterval.OneWeek.AlignFloor(monday));
    }

    [Fact]
    public void AlignmentIsIdempotent()
    {
        var value = At("2025-07-19T09:14:59Z");

        foreach (var interval in Enum.GetValues<CandleInterval>())
        {
            var once = interval.AlignFloor(value);
            var twice = interval.AlignFloor(once);

            Assert.Equal(once, twice);
            Assert.True(interval.IsAligned(once));
        }
    }

    [Fact]
    public void AlignmentNeverMovesForward()
    {
        var value = At("2025-07-19T09:14:59Z");

        foreach (var interval in Enum.GetValues<CandleInterval>())
        {
            Assert.True(interval.AlignFloor(value) <= value);
        }
    }

    [Fact]
    public void AnUnalignedInstantIsReportedAsUnaligned()
    {
        Assert.False(
            CandleInterval.OneHour.IsAligned(At("2025-03-05T13:37:00Z")));
    }

    [Fact]
    public void CloseTimeIsOneIntervalAfterOpen()
    {
        var candle = Candle.Of(
            CandleSource.BinanceFutures,
            "BTCUSDT",
            CandleInterval.FourHours,
            At("2025-03-05T12:00:00Z"),
            open: 1m,
            high: 2m,
            low: 0.5m,
            close: 1.5m,
            volume: 10m);

        Assert.Equal(At("2025-03-05T16:00:00Z"), candle.CloseTime);
    }

    [Fact]
    public void UnsupportedIntervalValuesAreRejectedRatherThanDefaulted()
    {
        const CandleInterval bogus = (CandleInterval)99;

        Assert.Throws<ArgumentOutOfRangeException>(() => bogus.Duration());
        Assert.Throws<ArgumentOutOfRangeException>(() => bogus.ToBinanceInterval());
    }

    private static DateTimeOffset At(string iso) =>
        DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture);
}
