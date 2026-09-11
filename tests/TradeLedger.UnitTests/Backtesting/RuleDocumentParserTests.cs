using TradeLedger.Core.Backtesting.Indicators;
using TradeLedger.Core.Backtesting.Rules;

namespace TradeLedger.UnitTests.Backtesting;

public class RuleDocumentParserTests
{
    private const string Valid = """
        {
          "version": 1,
          "indicators": [
            { "id": "emaFast", "type": "Ema", "source": "Close", "params": { "period": 21 } },
            { "id": "emaSlow", "type": "Ema", "source": "Close", "params": { "period": 55 } },
            { "id": "rsi", "type": "Rsi", "params": { "period": 14 } },
            { "id": "dmi", "type": "Dmi", "params": { "period": 14 } },
            { "id": "adx", "type": "Adx", "params": { "period": 14 } }
          ],
          "entry": {
            "long": {
              "op": "And",
              "operands": [
                { "op": "CrossesAbove", "left": { "ref": "emaFast" }, "right": { "ref": "emaSlow" } },
                { "op": "GreaterThan", "left": { "ref": "adx" }, "right": { "const": 20 } },
                { "op": "GreaterThan",
                  "left": { "ref": "dmi", "output": "PlusDi" },
                  "right": { "ref": "dmi", "output": "MinusDi" } },
                { "op": "LessThan", "left": { "ref": "rsi" }, "right": { "const": 70 } }
              ]
            },
            "short": null
          },
          "stopLoss": { "kind": "Percent", "percent": 1.5 }
        }
        """;

    [Fact]
    public void ReadsAFullyFormedStrategy()
    {
        var document = RuleDocumentParser.Parse(Valid);

        Assert.Equal(1, document.Version);
        Assert.Equal(5, document.Indicators.Count);
        Assert.NotNull(document.Entry.Long);
        Assert.Null(document.Entry.Short);
        Assert.IsType<PercentStop>(document.StopLoss);
    }

    [Fact]
    public void WarmupIsTheLongestAnyIndicatorNeeds()
    {
        var document = RuleDocumentParser.Parse(Valid);

        Assert.Equal(
            IndicatorFactory.WarmupBars("Ema", 55),
            document.WarmupBars);
    }

    [Fact]
    public void DefaultsThePriceSourceToClose()
    {
        var document = RuleDocumentParser.Parse(Rule(
            """{ "id": "sma", "type": "Sma", "params": { "period": 5 } }""",
            """{ "op": "GreaterThan", "left": { "ref": "sma" }, "right": { "const": 1 } }"""));

        Assert.Equal(PriceSource.Close, document.Indicators[0].Source);
    }

    [Theory]
    [InlineData("Sma")]
    [InlineData("Ema")]
    [InlineData("Rsi")]
    [InlineData("Adx")]
    public void AcceptsEverySingleOutputIndicator(string type)
    {
        var document = RuleDocumentParser.Parse(Rule(
            $$"""{ "id": "x", "type": "{{type}}", "params": { "period": 5 } }""",
            """{ "op": "GreaterThan", "left": { "ref": "x" }, "right": { "const": 1 } }"""));

        Assert.Equal(type, document.Indicators[0].Type);
    }

    [Fact]
    public void RejectsAnIndicatorOutsideTheSupportedFive()
    {
        var ex = Reject(Rule(
            """{ "id": "x", "type": "Macd", "params": { "period": 5 } }""",
            """{ "op": "GreaterThan", "left": { "ref": "x" }, "right": { "const": 1 } }"""));

        Assert.Contains("Macd", ex.Reason, StringComparison.Ordinal);
        Assert.Equal("$.indicators[0].type", ex.Path);
    }

    [Fact]
    public void RejectsASourceOnAnIndicatorThatUsesTheWholeBar()
    {
        var ex = Reject(Rule(
            """{ "id": "x", "type": "Adx", "source": "Close", "params": { "period": 5 } }""",
            """{ "op": "GreaterThan", "left": { "ref": "x" }, "right": { "const": 1 } }"""));

        Assert.Equal("$.indicators[0].source", ex.Path);
    }

    [Fact]
    public void RejectsDuplicateIndicatorIds()
    {
        var ex = Reject("""
            {
              "version": 1,
              "indicators": [
                { "id": "x", "type": "Sma", "params": { "period": 5 } },
                { "id": "x", "type": "Ema", "params": { "period": 5 } }
              ],
              "entry": { "long": { "op": "GreaterThan", "left": { "ref": "x" }, "right": { "const": 1 } } },
              "stopLoss": { "kind": "Percent", "percent": 2 }
            }
            """);

        Assert.Contains("Duplicate", ex.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsAReferenceToAnUndeclaredIndicator()
    {
        var ex = Reject(Rule(
            """{ "id": "x", "type": "Sma", "params": { "period": 5 } }""",
            """{ "op": "GreaterThan", "left": { "ref": "nope" }, "right": { "const": 1 } }"""));

        Assert.Contains("nope", ex.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsAnUnknownOutputName()
    {
        var ex = Reject(Rule(
            """{ "id": "d", "type": "Dmi", "params": { "period": 14 } }""",
            """{ "op": "GreaterThan", "left": { "ref": "d", "output": "Nope" }, "right": { "const": 1 } }"""));

        Assert.Contains("Nope", ex.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void RequiresAnOutputNameOnAMultiOutputIndicator()
    {
        var ex = Reject(Rule(
            """{ "id": "d", "type": "Dmi", "params": { "period": 14 } }""",
            """{ "op": "GreaterThan", "left": { "ref": "d" }, "right": { "const": 1 } }"""));

        Assert.Contains("several outputs", ex.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsANegativeOffsetBecauseItWouldReadTheFuture()
    {
        var ex = Reject(Rule(
            """{ "id": "x", "type": "Sma", "params": { "period": 5 } }""",
            """{ "op": "GreaterThan", "left": { "ref": "x", "offset": -1 }, "right": { "const": 1 } }"""));

        Assert.Contains("future", ex.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsAnOffsetBeyondTheCeiling()
    {
        var ex = Reject(Rule(
            """{ "id": "x", "type": "Sma", "params": { "period": 5 } }""",
            """{ "op": "GreaterThan", "left": { "ref": "x", "offset": 9999 }, "right": { "const": 1 } }"""));

        Assert.Contains("offset", ex.Path, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(5000)]
    public void RejectsAPeriodOutsideTheAllowedRange(int period)
    {
        var ex = Reject(Rule(
            $$"""{ "id": "x", "type": "Sma", "params": { "period": {{period}} } }""",
            """{ "op": "GreaterThan", "left": { "ref": "x" }, "right": { "const": 1 } }"""));

        Assert.Contains("period", ex.Path, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsAnUnknownParameter()
    {
        var ex = Reject(Rule(
            """{ "id": "x", "type": "Sma", "params": { "period": 5, "smoothing": 2 } }""",
            """{ "op": "GreaterThan", "left": { "ref": "x" }, "right": { "const": 1 } }"""));

        Assert.Contains("period", ex.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsAStrategyWithNoEntryAtAll()
    {
        var ex = Reject("""
            {
              "version": 1,
              "indicators": [ { "id": "x", "type": "Sma", "params": { "period": 5 } } ],
              "entry": { "long": null, "short": null },
              "stopLoss": { "kind": "Percent", "percent": 2 }
            }
            """);

        Assert.Equal("$.entry", ex.Path);
    }

    [Fact]
    public void RejectsAStrategyWithNoStopLoss()
    {
        var ex = Reject("""
            {
              "version": 1,
              "indicators": [ { "id": "x", "type": "Sma", "params": { "period": 5 } } ],
              "entry": { "long": { "op": "GreaterThan", "left": { "ref": "x" }, "right": { "const": 1 } } }
            }
            """);

        Assert.Equal("$.stopLoss", ex.Path);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.01)]
    [InlineData(80)]
    public void RejectsAnImplausibleStopPercentage(decimal percent)
    {
        var ex = Reject(Rule(
            """{ "id": "x", "type": "Sma", "params": { "period": 5 } }""",
            """{ "op": "GreaterThan", "left": { "ref": "x" }, "right": { "const": 1 } }""",
            $$"""{ "kind": "Percent", "percent": {{percent}} }"""));

        Assert.Equal("$.stopLoss.percent", ex.Path);
    }

    [Fact]
    public void ReadsAnIndicatorLevelStop()
    {
        var document = RuleDocumentParser.Parse(Rule(
            """{ "id": "x", "type": "Sma", "params": { "period": 5 } }""",
            """{ "op": "GreaterThan", "left": { "ref": "x" }, "right": { "const": 1 } }""",
            """{ "kind": "IndicatorLevel", "ref": "x", "bufferPercent": 0.5 }"""));

        var stop = Assert.IsType<IndicatorLevelStop>(document.StopLoss);

        Assert.Equal("x", stop.Ref);
        Assert.Equal(0.5m, stop.BufferPercent);
    }

    [Fact]
    public void RejectsAnIndicatorLevelStopPointingAtNothing()
    {
        var ex = Reject(Rule(
            """{ "id": "x", "type": "Sma", "params": { "period": 5 } }""",
            """{ "op": "GreaterThan", "left": { "ref": "x" }, "right": { "const": 1 } }""",
            """{ "kind": "IndicatorLevel", "ref": "ghost" }"""));

        Assert.Contains("ghost", ex.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsAnUnsupportedVersion()
    {
        var ex = Reject("""
            {
              "version": 7,
              "indicators": [ { "id": "x", "type": "Sma", "params": { "period": 5 } } ],
              "entry": { "long": { "op": "GreaterThan", "left": { "ref": "x" }, "right": { "const": 1 } } },
              "stopLoss": { "kind": "Percent", "percent": 2 }
            }
            """);

        Assert.Equal("$.version", ex.Path);
    }

    [Fact]
    public void RejectsAnUnknownOperator()
    {
        var ex = Reject(Rule(
            """{ "id": "x", "type": "Sma", "params": { "period": 5 } }""",
            """{ "op": "Approximately", "left": { "ref": "x" }, "right": { "const": 1 } }"""));

        Assert.Contains("Approximately", ex.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsALogicalNodeWithOnlyOneOperand()
    {
        var ex = Reject(Rule(
            """{ "id": "x", "type": "Sma", "params": { "period": 5 } }""",
            """
            { "op": "And", "operands": [
              { "op": "GreaterThan", "left": { "ref": "x" }, "right": { "const": 1 } }
            ] }
            """));

        Assert.Contains("at least two", ex.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsConditionsNestedTooDeeply()
    {
        var inner = """{ "op": "GreaterThan", "left": { "ref": "x" }, "right": { "const": 1 } }""";

        for (var i = 0; i < 25; i++)
        {
            inner = $$"""{ "op": "Not", "operand": {{inner}} }""";
        }

        var ex = Reject(Rule("""{ "id": "x", "type": "Sma", "params": { "period": 5 } }""", inner));

        Assert.Contains("deeper", ex.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsMalformedJson()
    {
        var ex = Assert.Throws<RuleValidationException>(
            () => RuleDocumentParser.Parse("{ not json"));

        Assert.Equal("$", ex.Path);
    }

    [Fact]
    public void RejectsAnEmptyIndicatorList()
    {
        var ex = Reject("""
            {
              "version": 1,
              "indicators": [],
              "entry": { "long": { "op": "GreaterThan", "left": { "price": "Close" }, "right": { "const": 1 } } },
              "stopLoss": { "kind": "Percent", "percent": 2 }
            }
            """);

        Assert.Equal("$.indicators", ex.Path);
    }

    [Fact]
    public void TheHashIgnoresKeyOrderAndWhitespace()
    {
        const string a = """
            { "version": 1, "indicators": [], "entry": {}, "stopLoss": { "kind": "Percent", "percent": 2 } }
            """;

        const string b = """
            {
              "stopLoss": { "percent": 2, "kind": "Percent" },
              "entry": {},
              "indicators": [],
              "version": 1
            }
            """;

        Assert.Equal(RuleDocumentParser.CanonicalHash(a), RuleDocumentParser.CanonicalHash(b));
    }

    [Fact]
    public void TheHashChangesWhenAValueChanges()
    {
        const string a = """{ "version": 1, "percent": 2 }""";
        const string b = """{ "version": 1, "percent": 3 }""";

        Assert.NotEqual(RuleDocumentParser.CanonicalHash(a), RuleDocumentParser.CanonicalHash(b));
    }

    private static RuleValidationException Reject(string json) =>
        Assert.Throws<RuleValidationException>(() => RuleDocumentParser.Parse(json));

    private static string Rule(
        string indicator,
        string condition,
        string stopLoss = """{ "kind": "Percent", "percent": 2 }""") =>
        $$"""
        {
          "version": 1,
          "indicators": [ {{indicator}} ],
          "entry": { "long": {{condition}} },
          "stopLoss": {{stopLoss}}
        }
        """;
}
