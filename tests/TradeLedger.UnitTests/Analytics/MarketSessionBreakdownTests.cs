using TradeLedger.Core.Analytics;
using TradeLedger.Core.Domain;

namespace TradeLedger.UnitTests.Analytics;

public class MarketSessionBreakdownTests
{
    [Theory]
    [InlineData(2, "Tokyo")]
    [InlineData(8, "Tokyo / London overlap")]
    [InlineData(14, "London / New York overlap")]
    [InlineData(22, "Off hours")]
    public void TheKeyComesFromTheDerivedSessionNotTheChecklistNote(int hour, string expected)
    {
        var trade = OpenedAt(hour);

        Assert.Equal(expected, AnalyticsService.KeyFor(trade, BreakdownDimension.MarketSession));
    }

    [Fact]
    public void ATradeWithNoChecklistStillLandsInItsSession()
    {
        var trade = OpenedAt(2);

        Assert.Null(trade.MarketContext);
        Assert.Equal("Tokyo", AnalyticsService.KeyFor(trade, BreakdownDimension.MarketSession));
    }

    [Fact]
    public void TheFreeTextChecklistNoteDoesNotOverrideTheDerivedSession()
    {
        var trade = OpenedAt(14);

        trade.Journal(new TradeJournalEdit
        {
            MarketContext = MarketContext.Create(marketSession: "asia, felt quiet"),
        });

        Assert.Equal("asia, felt quiet", trade.MarketContext!.MarketSession);
        Assert.Equal(
            "London / New York overlap",
            AnalyticsService.KeyFor(trade, BreakdownDimension.MarketSession));
    }

    private static Trade OpenedAt(int hour)
    {
        var opened = new DateTimeOffset(2026, 1, 5, hour, 0, 0, TimeSpan.Zero);

        return Trade.OpenManual(new NewManualTrade
        {
            AccountId = Guid.CreateVersion7(),
            Symbol = "BTCUSDT",
            Side = TradeSide.Long,
            EntryPrice = 100m,
            Quantity = 1m,
            OpenedAt = opened,
            ClosedAt = opened.AddMinutes(30),
            ExitPrice = 110m,
        });
    }
}
