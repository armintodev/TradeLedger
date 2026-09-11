using TradeLedger.Core.Backtesting.Indicators;
using TradeLedger.Core.Backtesting.Rules;
using TradeLedger.Core.Domain.MarketData;
using TradeLedger.Core.MarketData;

namespace TradeLedger.UnitTests.Backtesting;

public class RuleEvaluatorTests
{
    [Fact]
    public void AComparisonReadsTheCurrentBar()
    {
        var window = Window([10m, 20m, 30m]);
        window.MoveTo(1);

        Assert.True(Evaluate("""{ "op": "GreaterThan", "left": { "price": "Close" }, "right": { "const": 15 } }""", window));
        Assert.False(Evaluate("""{ "op": "GreaterThan", "left": { "price": "Close" }, "right": { "const": 25 } }""", window));
    }

    [Theory]
    [InlineData("GreaterThan", 15, true)]
    [InlineData("GreaterOrEqual", 20, true)]
    [InlineData("LessThan", 25, true)]
    [InlineData("LessOrEqual", 20, true)]
    [InlineData("EqualTo", 20, true)]
    [InlineData("NotEqualTo", 21, true)]
    [InlineData("GreaterThan", 20, false)]
    [InlineData("LessThan", 20, false)]
    [InlineData("EqualTo", 21, false)]
    public void EveryComparisonOperatorBehavesAsNamed(string op, int right, bool expected)
    {
        var window = Window([10m, 20m, 30m]);
        window.MoveTo(1);

        Assert.Equal(
            expected,
            Evaluate($$"""{ "op": "{{op}}", "left": { "price": "Close" }, "right": { "const": {{right}} } }""", window));
    }

    [Fact]
    public void AnOffsetReadsBackwardsInTime()
    {
        var window = Window([10m, 20m, 30m]);
        window.MoveTo(2);

        Assert.True(Evaluate(
            """{ "op": "EqualTo", "left": { "price": "Close", "offset": 2 }, "right": { "const": 10 } }""",
            window));

        Assert.True(Evaluate(
            """{ "op": "EqualTo", "left": { "price": "Close", "offset": 0 }, "right": { "const": 30 } }""",
            window));
    }

    [Fact]
    public void AnOffsetBeforeTheStartOfDataIsFalseRatherThanThrowing()
    {
        var window = Window([10m, 20m, 30m]);
        window.MoveTo(0);

        Assert.False(Evaluate(
            """{ "op": "GreaterThan", "left": { "price": "Close", "offset": 5 }, "right": { "const": 0 } }""",
            window));
    }

    [Fact]
    public void TheWindowRefusesToLookForwardEvenIfAskedDirectly()
    {
        var window = Window([10m, 20m, 30m]);
        window.MoveTo(0);

        var operand = new PriceOperand(PriceSource.Close, 0);

        Assert.Throws<LookaheadException>(() => window.Value(operand, -1));
    }

    [Fact]
    public void AndRequiresEveryOperand()
    {
        var window = Window([10m, 20m, 30m]);
        window.MoveTo(1);

        Assert.True(Evaluate(And("15", "10"), window));
        Assert.False(Evaluate(And("15", "25"), window));
    }

    [Fact]
    public void OrAcceptsAnySingleOperand()
    {
        var window = Window([10m, 20m, 30m]);
        window.MoveTo(1);

        Assert.True(Evaluate(Or("15", "25"), window));
        Assert.False(Evaluate(Or("25", "30"), window));
    }

    [Fact]
    public void NotInvertsItsOperand()
    {
        var window = Window([10m, 20m, 30m]);
        window.MoveTo(1);

        Assert.False(Evaluate(
            """{ "op": "Not", "operand": { "op": "GreaterThan", "left": { "price": "Close" }, "right": { "const": 15 } } }""",
            window));
    }

    [Fact]
    public void BetweenIsInclusiveAtBothEnds()
    {
        var window = Window([10m, 20m, 30m]);
        window.MoveTo(1);

        Assert.True(Evaluate(Between(20, 30), window));
        Assert.True(Evaluate(Between(10, 20), window));
        Assert.False(Evaluate(Between(21, 30), window));
    }

    [Fact]
    public void CrossesAboveNeedsThePreviousBarOnTheOtherSide()
    {
        var window = Window([10m, 20m, 30m]);

        window.MoveTo(1);
        Assert.True(Evaluate(Cross("CrossesAbove", 15), window));

        window.MoveTo(2);
        Assert.False(Evaluate(Cross("CrossesAbove", 15), window));
    }

    [Fact]
    public void CrossesBelowNeedsThePreviousBarAbove()
    {
        var window = Window([30m, 20m, 10m]);

        window.MoveTo(1);
        Assert.True(Evaluate(Cross("CrossesBelow", 25), window));

        window.MoveTo(2);
        Assert.False(Evaluate(Cross("CrossesBelow", 25), window));
    }

    [Fact]
    public void ACrossIsFalseOnTheFirstEvaluableBar()
    {
        var window = Window([10m, 20m, 30m]);
        window.MoveTo(0);

        Assert.False(Evaluate(Cross("CrossesAbove", 5), window));
    }

    [Fact]
    public void TouchingWithoutCrossingIsNotACross()
    {
        var window = Window([15m, 15m, 20m]);
        window.MoveTo(1);

        Assert.False(Evaluate(Cross("CrossesAbove", 15), window));
    }

    [Fact]
    public void RisingForRequiresStrictlyIncreasingBars()
    {
        var rising = Window([10m, 20m, 30m, 40m]);
        rising.MoveTo(3);

        Assert.True(Evaluate(Trend("RisingFor", 3), rising));

        var flat = Window([10m, 20m, 20m, 40m]);
        flat.MoveTo(3);

        Assert.False(Evaluate(Trend("RisingFor", 3), flat));
    }

    [Fact]
    public void FallingForRequiresStrictlyDecreasingBars()
    {
        var falling = Window([40m, 30m, 20m, 10m]);
        falling.MoveTo(3);

        Assert.True(Evaluate(Trend("FallingFor", 3), falling));

        var mixed = Window([40m, 30m, 35m, 10m]);
        mixed.MoveTo(3);

        Assert.False(Evaluate(Trend("FallingFor", 3), mixed));
    }

    [Fact]
    public void ATrendRunningOffTheStartOfDataIsFalse()
    {
        var window = Window([10m, 20m]);
        window.MoveTo(1);

        Assert.False(Evaluate(Trend("RisingFor", 5), window));
    }

    [Fact]
    public void AnIndicatorThatIsNotYetWarmMakesTheConditionFalse()
    {
        var candles = Candles([10m, 20m, 30m, 40m]);
        var series = new Dictionary<string, IndicatorSeries>
        {
            ["sma"] = IndicatorFactory.Compute("Sma", 3, PriceSource.Close, candles),
        };

        var window = new BarWindow(candles, series);
        window.MoveTo(0);

        Assert.False(Evaluate(
            """{ "op": "GreaterThan", "left": { "ref": "sma" }, "right": { "const": 0 } }""",
            window,
            """{ "id": "sma", "type": "Sma", "params": { "period": 3 } }"""));
    }

    [Fact]
    public void SubOutputsOfAMultiOutputIndicatorAreAddressable()
    {
        var candles = Candles([10m, 20m, 15m, 25m, 18m, 30m, 22m, 35m]);
        var series = new Dictionary<string, IndicatorSeries>
        {
            ["dmi"] = IndicatorFactory.Compute("Dmi", 2, PriceSource.Close, candles),
        };

        var window = new BarWindow(candles, series);
        window.MoveTo(candles.Count - 1);

        var plus = window.Value(new IndicatorOperand("dmi", "PlusDi", 0));
        var minus = window.Value(new IndicatorOperand("dmi", "MinusDi", 0));

        Assert.NotNull(plus);
        Assert.NotNull(minus);
        Assert.NotEqual(plus, minus);
    }

    [Fact]
    public void AndShortCircuitsOnTheFirstFalseOperand()
    {
        var window = Window([10m, 20m, 30m]);
        window.MoveTo(1);

        Assert.False(Evaluate(And("25", "0"), window));
    }

    private static bool Evaluate(
        string condition,
        BarWindow window,
        string indicator = """{ "id": "sma", "type": "Sma", "params": { "period": 1 } }""")
    {
        var document = RuleDocumentParser.Parse($$"""
            {
              "version": 1,
              "indicators": [ {{indicator}} ],
              "entry": { "long": {{condition}} },
              "stopLoss": { "kind": "Percent", "percent": 2 }
            }
            """);

        return RuleEvaluator.Evaluate(document.Entry.Long!, window);
    }

    private static string And(string first, string second) =>
        $$"""
        { "op": "And", "operands": [
          { "op": "GreaterThan", "left": { "price": "Close" }, "right": { "const": {{first}} } },
          { "op": "GreaterThan", "left": { "price": "Close" }, "right": { "const": {{second}} } }
        ] }
        """;

    private static string Or(string first, string second) =>
        $$"""
        { "op": "Or", "operands": [
          { "op": "GreaterThan", "left": { "price": "Close" }, "right": { "const": {{first}} } },
          { "op": "GreaterThan", "left": { "price": "Close" }, "right": { "const": {{second}} } }
        ] }
        """;

    private static string Between(int low, int high) =>
        $$"""
        { "op": "Between", "left": { "price": "Close" },
          "low": { "const": {{low}} }, "high": { "const": {{high}} } }
        """;

    private static string Cross(string op, int level) =>
        $$"""{ "op": "{{op}}", "left": { "price": "Close" }, "right": { "const": {{level}} } }""";

    private static string Trend(string op, int bars) =>
        $$"""{ "op": "{{op}}", "operand": { "price": "Close" }, "bars": {{bars}} }""";

    private static BarWindow Window(decimal[] closes) =>
        new(Candles(closes), new Dictionary<string, IndicatorSeries>());

    private static List<Candle> Candles(decimal[] closes)
    {
        var candles = new List<Candle>(closes.Length);

        for (var i = 0; i < closes.Length; i++)
        {
            candles.Add(Candle.Of(
                CandleSource.BinanceFutures,
                "BTCUSDT",
                CandleInterval.OneHour,
                DateTimeOffset.UnixEpoch.AddHours(i),
                closes[i],
                closes[i] + 1m,
                closes[i] - 1m,
                closes[i],
                volume: 100m));
        }

        return candles;
    }
}
