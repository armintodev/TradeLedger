using System.Globalization;
using System.Text.Json;
using TradeLedger.Core.Domain;
using TradeLedger.Core.Integrations.Bitunix;
using TradeLedger.Core.Integrations.Bitunix.Dtos;

namespace TradeLedger.UnitTests;

public class BitunixMappingTests
{
    private static readonly Guid AccountId = Guid.CreateVersion7();

    [Fact]
    public void Deserialize_ParsesNumericStringsIntoDecimals()
    {
        const string json = """
            {
              "positionId": "123",
              "symbol": "BTCUSDT",
              "maxQty": "0.005",
              "entryPrice": "64250.5",
              "closePrice": "65100.25",
              "liqQty": "0",
              "side": "LONG",
              "marginMode": "ISOLATION",
              "positionMode": "ONE_WAY",
              "leverage": 10,
              "fee": "0.42",
              "funding": "-0.08",
              "realizedPNL": "4.25",
              "ctime": 1735689600000,
              "mtime": 1735693200000
            }
            """;

        var dto = JsonSerializer.Deserialize<HistoryPositionDto>(json, BitunixJson.Options);

        Assert.NotNull(dto);
        Assert.Equal(0.005m, dto.MaxQty);
        Assert.Equal(64250.5m, dto.EntryPrice);
        Assert.Equal(-0.08m, dto.Funding);
        Assert.Equal(4.25m, dto.RealizedPnl);
    }

    [Fact]
    public void Deserialize_IsUnaffectedByAmbientCulture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            var dto = JsonSerializer.Deserialize<HistoryPositionDto>(
                """{"positionId":"1","symbol":"BTCUSDT","entryPrice":"1234.56"}""",
                BitunixJson.Options);

            Assert.Equal(1234.56m, dto!.EntryPrice);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void ApplyExchangeSnapshot_MapsMechanicalFieldsAndDerivesNet()
    {
        var trade = NewSyncedTrade();

        Assert.Equal("BTCUSDT", trade.Symbol);
        Assert.Equal(TradeSide.Long, trade.Side);
        Assert.Equal(10, trade.Leverage);
        Assert.Equal(TradeOrigin.Synced, trade.Origin);
        Assert.Equal(MarginMode.Isolated, trade.MarginMode);

        Assert.Equal(3.75m, trade.NetProfitLoss);
        Assert.Equal(TradeOutcome.Win, trade.Outcome);
    }

    [Fact]
    public void ApplyExchangeSnapshot_PreservesTheSubjectiveHalfOnResync()
    {
        var strategyId = Guid.CreateVersion7();
        var mentalStateId = Guid.CreateVersion7();

        var trade = NewSyncedTrade();

        trade.Journal(new TradeJournalEdit
        {
            StrategyId = strategyId,
            EntryMentalStateId = mentalStateId,
            Rating = 4,
            Memo = "Waited for the retest.",
            MarketContext = MarketContext.Create(marketSession: "London", rsi: "42"),
            MarkReviewed = true,
        });

        trade.ApplyExchangeSnapshot(BitunixPositionMapper.ToSnapshot(SamplePosition()));

        Assert.Equal(strategyId, trade.StrategyId);
        Assert.Equal(mentalStateId, trade.EntryMentalStateId);
        Assert.Equal(4, trade.Rating);
        Assert.Equal("Waited for the retest.", trade.Memo);
        Assert.Equal(ReviewState.Reviewed, trade.ReviewState);
        Assert.Equal("London", trade.MarketContext!.MarketSession);
    }

    [Fact]
    public void ApplyExchangeSnapshot_ConvertsEpochMillisecondsToUtcInstants()
    {
        var trade = NewSyncedTrade();

        Assert.Equal(
            DateTimeOffset.FromUnixTimeMilliseconds(1735689600000), trade.OpenedAt);
        Assert.Equal(1735689600000, trade.OpenedAtRawMs);
        Assert.Equal(1735693200000, trade.ClosedAtRawMs);
    }

    [Fact]
    public void ApplyExchangeSnapshot_TreatsFeeAsACostRegardlessOfReportedSign()
    {
        var dto = SamplePosition();
        dto.Fee = -0.42m;

        var trade = NewSyncedTrade(dto);

        Assert.Equal(0.42m, trade.Fees);
    }

    [Fact]
    public void ApplyExchangeSnapshot_DerivesTheMarketSessionFromTheOpenInstant()
    {
        var trade = NewSyncedTrade();

        Assert.Equal(MarketSession.Tokyo, trade.MarketSession);
    }

    [Fact]
    public void AManualTradeRefusesAnExchangeSnapshot()
    {
        var manual = Trade.OpenManual(new NewManualTrade
        {
            AccountId = AccountId,
            Symbol = "BTCUSDT",
            Side = TradeSide.Long,
            OpenedAt = DateTimeOffset.UnixEpoch,
            EntryPrice = 100m,
            Quantity = 1m,
        });

        var thrown = Assert.Throws<DomainRuleException>(
            () => manual.ApplyExchangeSnapshot(BitunixPositionMapper.ToSnapshot(SamplePosition())));

        Assert.Equal("manual_trade_from_exchange", thrown.Code);
    }

    [Fact]
    public void ToSnapshot_ExcludesBonusFromEquity()
    {
        var dto = new FuturesAccountDto
        {
            MarginCoin = "USDT",
            Available = 500m,
            Frozen = 100m,
            Margin = 400m,
            CrossUnrealizedPnl = 25m,
            IsolationUnrealizedPnl = 5m,
            Bonus = 50m,
        };

        var snapshot = BalanceSnapshot.Capture(
            BitunixPositionMapper.ToSnapshot(dto, AccountId, Guid.CreateVersion7()));

        Assert.Equal(1000m, snapshot.WalletBalance);
        Assert.Equal(30m, snapshot.UnrealizedPnl);
        Assert.Equal(1030m, snapshot.Equity);
        Assert.Equal(50m, snapshot.Bonus);
    }

    private static Trade NewSyncedTrade(HistoryPositionDto? dto = null) =>
        Trade.FromExchange(
            Guid.CreateVersion7(),
            AccountId,
            BitunixPositionMapper.ToSnapshot(dto ?? SamplePosition()));

    private static HistoryPositionDto SamplePosition() => new()
    {
        PositionId = "123",
        Symbol = "BTCUSDT",
        MaxQty = 0.005m,
        EntryPrice = 64250.5m,
        ClosePrice = 65100.25m,
        Side = "LONG",
        MarginMode = "ISOLATION",
        PositionMode = "ONE_WAY",
        Leverage = 10,
        Fee = 0.42m,
        Funding = -0.08m,
        RealizedPnl = 4.25m,
        Ctime = 1735689600000,
        Mtime = 1735693200000,
    };
}
