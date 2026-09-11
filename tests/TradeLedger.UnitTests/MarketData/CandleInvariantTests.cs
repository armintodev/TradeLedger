using TradeLedger.Core.Domain;
using TradeLedger.Core.Domain.MarketData;

namespace TradeLedger.UnitTests.MarketData;

public class CandleInvariantTests
{
    private static readonly DateTimeOffset OpenTime = DateTimeOffset.UnixEpoch;

    [Fact]
    public void AWellFormedBarIsAccepted()
    {
        var candle = Bar(open: 100m, high: 110m, low: 95m, close: 105m);

        Assert.Equal("BTCUSDT", candle.Symbol);
        Assert.Equal(15m, candle.Range);
        Assert.True(candle.IsUp);
    }

    [Fact]
    public void AHighBelowItsLowIsRejected()
    {
        var thrown = Assert.Throws<DomainRuleException>(
            () => Bar(open: 100m, high: 90m, low: 95m, close: 98m));

        Assert.Equal("candle_high_below_low", thrown.Code);
    }

    [Fact]
    public void AnOpenOutsideTheBarRangeIsRejected()
    {
        var thrown = Assert.Throws<DomainRuleException>(
            () => Bar(open: 120m, high: 110m, low: 95m, close: 105m));

        Assert.Equal("candle_open_outside_range", thrown.Code);
    }

    [Fact]
    public void ACloseOutsideTheBarRangeIsRejected()
    {
        var thrown = Assert.Throws<DomainRuleException>(
            () => Bar(open: 100m, high: 110m, low: 95m, close: 94m));

        Assert.Equal("candle_close_outside_range", thrown.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ANonPositivePriceIsRejected(int close)
    {
        Assert.Throws<DomainValidationException>(
            () => Bar(open: 100m, high: 110m, low: 95m, close: close));
    }

    [Fact]
    public void ANegativeVolumeIsRejected()
    {
        Assert.Throws<DomainValidationException>(
            () => Candle.Of(
                CandleSource.BinanceFutures,
                "BTCUSDT",
                CandleInterval.OneHour,
                OpenTime,
                100m,
                110m,
                95m,
                105m,
                volume: -1m));
    }

    [Fact]
    public void ADojiWhereEveryPriceIsEqualIsStillValid()
    {
        var candle = Bar(open: 100m, high: 100m, low: 100m, close: 100m);

        Assert.Equal(0m, candle.Range);
        Assert.False(candle.IsUp);
    }

    [Fact]
    public void TheSymbolIsNormalisedSoOneMarketIsNotStoredTwice()
    {
        Assert.Equal("BTCUSDT", Bar(symbol: "btcusdt").Symbol);
    }

    [Fact]
    public void TheRawEpochIsDerivedFromTheOpenTimeWhenTheSourceDoesNotSupplyIt()
    {
        var candle = Bar();

        Assert.Equal(OpenTime.ToUnixTimeMilliseconds(), candle.OpenTimeRawMs);
    }

    [Fact]
    public void CloseTimeIsOneIntervalAfterOpen()
    {
        Assert.Equal(OpenTime.AddHours(1), Bar().CloseTime);
    }

    private static Candle Bar(
        decimal open = 100m,
        decimal high = 110m,
        decimal low = 95m,
        decimal close = 105m,
        string symbol = "BTCUSDT") =>
        Candle.Of(
            CandleSource.BinanceFutures,
            symbol,
            CandleInterval.OneHour,
            OpenTime,
            open,
            high,
            low,
            close,
            volume: 1000m);
}
