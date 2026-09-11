using TradeLedger.Core.Backtesting;
using TradeLedger.Core.Backtesting.Engine;
using TradeLedger.Core.Backtesting.Rules;
using TradeLedger.Core.Domain;
using TradeLedger.Core.Domain.Backtesting;
using TradeLedger.Core.Domain.MarketData;
using TradeLedger.Core.MarketData;

namespace TradeLedger.UnitTests.Backtesting;

public class RuleSimulationCoreTests
{
    private const string CrossAboveSmaWithTwoPercentStop = """
        {
          "version": 1,
          "indicators": [
            { "id": "sma", "type": "Sma", "source": "Close", "params": { "period": 2 } }
          ],
          "entry": {
            "long": {
              "op": "CrossesAbove",
              "left": { "price": "Close" },
              "right": { "ref": "sma" }
            }
          },
          "stopLoss": { "kind": "Percent", "percent": 2 }
        }
        """;

    [Fact]
    public async Task ASignalFillsAtTheNextBarsOpenNotTheSignalBarsClose()
    {
        var bars = Series(
            (100m, 100m, 100m, 100m),
            (100m, 100m, 100m, 100m),
            (99m, 99m, 99m, 99m),
            (99m, 99m, 99m, 99m),
            (101m, 101m, 101m, 101m),
            (130m, 131m, 120m, 121m),
            (121m, 121m, 121m, 121m));

        var result = await RunAsync(bars);

        var trade = Assert.Single(result.Trades);

        Assert.Equal(130m, trade.EntryPrice);
        Assert.Equal(5, trade.EntryBarIndex);
        Assert.Equal(TradeOutcome.Loss, trade.Outcome);
        Assert.Equal(BacktestExitReason.StopLoss, trade.ExitReason);
    }

    [Fact]
    public async Task AStrategyThatOnlyWinsByPeekingAtTheNextBarLosesHonestly()
    {
        var bars = Series(
            (100m, 100m, 100m, 100m),
            (100m, 100m, 100m, 100m),
            (99m, 99m, 99m, 99m),
            (99m, 99m, 99m, 99m),
            (101m, 101m, 101m, 101m),
            (130m, 131m, 120m, 121m),
            (121m, 121m, 121m, 121m));

        var result = await RunAsync(bars);

        var trade = Assert.Single(result.Trades);

        Assert.True(
            trade.NetProfitLoss < 0,
            "Filling at the signal bar's close of 100 would have put the target at 104, which the " +
            $"next bar's high of 131 clears for a win. Entering honestly at the next open of 130 " +
            $"stops out. The engine reported {trade.NetProfitLoss}, so it is reading the future.");

        Assert.Equal(-1m, trade.AchievedReturnR);
        Assert.Equal(9_800m, result.ClosingBalance);
    }

    [Fact]
    public async Task AWinnerPaysExactlyTheConfiguredRiskRewardMultiple()
    {
        var bars = Series(
            (100m, 100m, 100m, 100m),
            (100m, 100m, 100m, 100m),
            (99m, 99m, 99m, 99m),
            (99m, 99m, 99m, 99m),
            (101m, 101m, 101m, 101m),
            (100m, 105m, 99.5m, 104m),
            (104m, 104m, 104m, 104m));

        var result = await RunAsync(bars);

        var trade = Assert.Single(result.Trades);

        Assert.Equal(TradeOutcome.Win, trade.Outcome);
        Assert.Equal(BacktestExitReason.TakeProfit, trade.ExitReason);
        Assert.Equal(2m, trade.AchievedReturnR);
        Assert.Equal(2m, trade.PlannedReturnR);
        Assert.Equal(400m, trade.NetProfitLoss);
        Assert.Equal(10_400m, result.ClosingBalance);
    }

    [Fact]
    public async Task TheStopSitsExactlyThePercentageBelowTheEntryFill()
    {
        var bars = WinningSeries();

        var trade = (await RunAsync(bars)).Trades[0];

        Assert.Equal(98m, trade.StopLossPrice);
        Assert.Equal(104m, trade.TakeProfitPrice);
        Assert.Equal(100m, trade.EntryPrice);
    }

    [Fact]
    public async Task ALoserCostsExactlyTheConfiguredRiskPercentage()
    {
        var bars = Series(
            (100m, 100m, 100m, 100m),
            (100m, 100m, 100m, 100m),
            (99m, 99m, 99m, 99m),
            (99m, 99m, 99m, 99m),
            (101m, 101m, 101m, 101m),
            (100m, 101m, 97m, 98m),
            (98m, 98m, 98m, 98m));

        var result = await RunAsync(bars);
        var trade = Assert.Single(result.Trades);

        Assert.Equal(BacktestExitReason.StopLoss, trade.ExitReason);
        Assert.Equal(-200m, trade.NetProfitLoss);
        Assert.Equal(-1m, trade.AchievedReturnR);
        Assert.Equal(9_800m, result.ClosingBalance);
    }

    [Fact]
    public async Task RiskCompoundsSoASecondTradeIsSizedOffTheGrownBalance()
    {
        var bars = Series(
            (100m, 100m, 100m, 100m),
            (100m, 100m, 100m, 100m),
            (99m, 99m, 99m, 99m),
            (99m, 99m, 99m, 99m),
            (101m, 101m, 101m, 101m),
            (100m, 105m, 99.5m, 104m),
            (104m, 104m, 104m, 104m),
            (103m, 103m, 103m, 103m),
            (103m, 103m, 103m, 103m),
            (105m, 105m, 105m, 105m),
            (100m, 105m, 99.5m, 104m),
            (104m, 104m, 104m, 104m));

        var result = await RunAsync(bars);

        Assert.Equal(2, result.Trades.Count);

        var first = result.Trades[0];
        var second = result.Trades[1];

        Assert.Equal(10_400m, first.BalanceAfter);
        Assert.True(
            second.Quantity > first.Quantity,
            "The second position should be larger because risk is a percentage of the grown balance.");

        Assert.Equal(10_400m * 0.02m, second.Quantity * Math.Abs(second.EntryPrice - second.StopLossPrice));
    }

    [Fact]
    public async Task TheSameInputsProduceIdenticalResultsEveryTime()
    {
        var bars = WinningSeries();

        var first = await RunAsync(bars);
        var second = await RunAsync(bars);

        Assert.Equal(first.ClosingBalance, second.ClosingBalance);
        Assert.Equal(first.Trades.Count, second.Trades.Count);

        for (var i = 0; i < first.Trades.Count; i++)
        {
            Assert.Equal(first.Trades[i].EntryPrice, second.Trades[i].EntryPrice);
            Assert.Equal(first.Trades[i].ExitPrice, second.Trades[i].ExitPrice);
            Assert.Equal(first.Trades[i].NetProfitLoss, second.Trades[i].NetProfitLoss);
            Assert.Equal(first.Trades[i].Sequence, second.Trades[i].Sequence);
        }
    }

    [Fact]
    public async Task AnOpenPositionAtTheEndOfTheRangeIsClosedAndCounted()
    {
        var bars = Series(
            (100m, 100m, 100m, 100m),
            (100m, 100m, 100m, 100m),
            (99m, 99m, 99m, 99m),
            (99m, 99m, 99m, 99m),
            (101m, 101m, 101m, 101m),
            (100m, 101m, 99.5m, 100.5m),
            (100.5m, 101m, 99.5m, 100.5m));

        var result = await RunAsync(bars);

        var trade = Assert.Single(result.Trades);

        Assert.Equal(BacktestExitReason.EndOfData, trade.ExitReason);
        Assert.Equal(1, result.Summary.OpenAtEndOfData);
    }

    [Fact]
    public async Task TwoSignalsOnTheSameBarTakeNeitherAndAreCounted()
    {
        const string bothSides = """
            {
              "version": 1,
              "indicators": [
                { "id": "sma", "type": "Sma", "source": "Close", "params": { "period": 2 } }
              ],
              "entry": {
                "long": { "op": "GreaterThan", "left": { "price": "Close" }, "right": { "const": 0 } },
                "short": { "op": "GreaterThan", "left": { "price": "Close" }, "right": { "const": 0 } }
              },
              "stopLoss": { "kind": "Percent", "percent": 2 }
            }
            """;

        var result = await RunAsync(WinningSeries(), bothSides);

        Assert.Empty(result.Trades);
        Assert.True(result.Summary.AmbiguousSignals > 0);
    }

    [Fact]
    public async Task AnIndicatorStopOnTheWrongSideOfEntryIsSkippedNotSubstituted()
    {
        const string stopAboveEntry = """
            {
              "version": 1,
              "indicators": [
                { "id": "sma", "type": "Sma", "source": "Close", "params": { "period": 2 } }
              ],
              "entry": {
                "long": {
                  "op": "CrossesAbove",
                  "left": { "price": "Close" },
                  "right": { "ref": "sma" }
                }
              },
              "stopLoss": { "kind": "IndicatorLevel", "ref": "sma", "bufferPercent": 0 }
            }
            """;

        var bars = Series(
            (100m, 100m, 100m, 100m),
            (100m, 100m, 100m, 100m),
            (99m, 99m, 99m, 99m),
            (99m, 99m, 99m, 99m),
            (101m, 101m, 101m, 101m),
            (95m, 96m, 94m, 95m),
            (95m, 95m, 95m, 95m));

        var result = await RunAsync(bars, stopAboveEntry);

        Assert.Empty(result.Trades);
        Assert.Equal(1, result.Summary.SkippedInvalidStop);
    }

    [Fact]
    public async Task TheEquityCurveOpensAtTheStartingBalance()
    {
        var result = await RunAsync(WinningSeries());

        Assert.Equal(10_000m, result.EquityPoints[0].Equity);
        Assert.Equal(0, result.EquityPoints[0].Sequence);
        Assert.Equal(10_400m, result.EquityPoints[^1].Equity);
    }

    [Fact]
    public async Task ExitsAreRecordedAsExecutionsAlongsideEntries()
    {
        var trade = (await RunAsync(WinningSeries())).Trades[0];

        Assert.Equal(2, trade.Executions.Count);
        Assert.Contains(trade.Executions, e => e.Role == ExecutionRole.Open);
        Assert.Contains(trade.Executions, e => e.Role == ExecutionRole.Close);
    }

    [Fact]
    public async Task FeesAndSlippageMakeTheSameTradeWorse()
    {
        var bars = WinningSeries();

        var frictionless = await RunAsync(bars);
        var realistic = await RunAsync(bars, costs: new CostModel(0.0006m, 0.0002m, 0.0005m));

        Assert.True(realistic.ClosingBalance < frictionless.ClosingBalance);
        Assert.True(realistic.Trades[0].Fees > 0);
    }

    [Fact]
    public async Task AmbiguousBarsAreCountedAsAssumedWhenNoMinuteDataExists()
    {
        var bars = Series(
            (100m, 100m, 100m, 100m),
            (100m, 100m, 100m, 100m),
            (99m, 99m, 99m, 99m),
            (99m, 99m, 99m, 99m),
            (101m, 101m, 101m, 101m),
            (100m, 105m, 97m, 99m),
            (99m, 99m, 99m, 99m));

        var result = await RunAsync(bars);

        Assert.Equal(1, result.Summary.AssumedNoMinuteData);
        Assert.Equal(
            IntrabarResolution.AssumedNoMinuteData,
            result.Trades[0].IntrabarResolution);
        Assert.Equal(BacktestExitReason.StopLoss, result.Trades[0].ExitReason);
    }

    [Fact]
    public async Task MinuteDataTurnsAnAssumedExitIntoAResolvedOne()
    {
        var bars = Series(
            (100m, 100m, 100m, 100m),
            (100m, 100m, 100m, 100m),
            (99m, 99m, 99m, 99m),
            (99m, 99m, 99m, 99m),
            (101m, 101m, 101m, 101m),
            (100m, 105m, 97m, 99m),
            (99m, 99m, 99m, 99m));

        var minutes = new StubMinutes(
        [
            Minute(bars[5].OpenTime, 0, 100m, 105m),
            Minute(bars[5].OpenTime, 1, 97m, 100m),
        ]);

        var result = await RunAsync(bars, minutes: minutes);

        Assert.Equal(1, result.Summary.ResolvedByMinute);
        Assert.Equal(BacktestExitReason.TakeProfit, result.Trades[0].ExitReason);
        Assert.Equal(TradeOutcome.Win, result.Trades[0].Outcome);
    }

    [Fact]
    public async Task AMostlyAssumedRunWarnsThatItCannotBeTrusted()
    {
        var bars = Series(
            (100m, 100m, 100m, 100m),
            (100m, 100m, 100m, 100m),
            (99m, 99m, 99m, 99m),
            (99m, 99m, 99m, 99m),
            (101m, 101m, 101m, 101m),
            (100m, 105m, 97m, 99m),
            (99m, 99m, 99m, 99m));

        var result = await RunAsync(bars);

        Assert.Contains(
            result.Warnings,
            w => w.Contains("assumed", StringComparison.OrdinalIgnoreCase));
    }

    private static List<Candle> WinningSeries() => Series(
        (100m, 100m, 100m, 100m),
        (100m, 100m, 100m, 100m),
        (99m, 99m, 99m, 99m),
        (99m, 99m, 99m, 99m),
        (101m, 101m, 101m, 101m),
        (100m, 105m, 99.5m, 104m),
        (104m, 104m, 104m, 104m));

    private static async Task<BacktestEngineResult> RunAsync(
        List<Candle> bars,
        string rule = CrossAboveSmaWithTwoPercentStop,
        CostModel? costs = null,
        IMinuteCandleSource? minutes = null)
    {
        var document = RuleDocumentParser.Parse(rule);

        var parameters = new SimulationParameters(
            Symbol: "BTCUSDT",
            From: bars[0].OpenTime,
            OpeningBalance: 10_000m,
            RiskPercentPerPosition: 2m,
            RiskRewardRatio: 2m,
            Leverage: 5,
            MaintenanceMarginRate: 0.005m,
            Costs: costs ?? new CostModel(0m, 0m, 0m));

        return await RuleSimulationCore.RunAsync(
            document,
            bars,
            startIndex: 2,
            parameters,
            funding: [],
            minutes ?? NoMinuteCandles.Instance,
            NoProgress.Instance,
            warnings: []);
    }

    private static List<Candle> Series(params (decimal Open, decimal High, decimal Low, decimal Close)[] rows)
    {
        var candles = new List<Candle>(rows.Length);

        for (var i = 0; i < rows.Length; i++)
        {
            var openTime = DateTimeOffset.UnixEpoch.AddHours(i);

            candles.Add(Candle.Of(
                CandleSource.BinanceFutures,
                "BTCUSDT",
                CandleInterval.OneHour,
                openTime,
                rows[i].Open,
                rows[i].High,
                rows[i].Low,
                rows[i].Close,
                volume: 1000m));
        }

        return candles;
    }

    private static Candle Minute(DateTimeOffset barOpen, int offset, decimal low, decimal high) =>
        Candle.Of(
            CandleSource.BinanceFutures,
            "BTCUSDT",
            CandleInterval.OneMinute,
            barOpen.AddMinutes(offset),
            low,
            high,
            low,
            high,
            volume: 10m);

    private sealed class StubMinutes(IReadOnlyList<Candle> candles) : IMinuteCandleSource
    {
        public Task<IReadOnlyList<Candle>> GetAsync(
            DateTimeOffset from,
            DateTimeOffset to,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Candle>>(
                [.. candles.Where(c => c.OpenTime >= from && c.OpenTime < to)]);
    }
}
