using TradeLedger.Core.Backtesting.Indicators;
using TradeLedger.Core.Backtesting.Rules;
using TradeLedger.Core.Domain.MarketData;

namespace TradeLedger.UnitTests.Backtesting;

public class RuleDocumentVersionTests
{
    [Fact]
    public void AVersionOneDocumentStillParses()
    {
        // Every completed run stores its own frozen copy of the document it executed, so a
        // version that stopped parsing would make historical results unexplainable.
        var document = Parse(1, """{ "id": "ema", "type": "Ema", "params": { "period": 20 } }""");

        var spec = Assert.Single(document.Indicators);

        Assert.Null(spec.Interval);
        Assert.False(document.IsMultiTimeframe);
    }

    [Fact]
    public void AVersionOneDocumentCarryingAnIntervalIsRefusedRatherThanReinterpreted()
    {
        // The key parsed and was ignored before version 2. Honouring it now would give the
        // same bytes a new meaning under an unchanged hash.
        var ex = Assert.Throws<RuleValidationException>(() => Parse(
            1,
            """{ "id": "adx", "type": "Adx", "interval": "FourHours", "params": { "period": 14 } }"""));

        Assert.Contains("version 2", ex.Reason);
    }

    [Fact]
    public void AVersionTwoDocumentAcceptsAnInterval()
    {
        var document = Parse(
            2,
            """{ "id": "adx", "type": "Adx", "interval": "FourHours", "params": { "period": 14 } }""");

        Assert.Equal(CandleInterval.FourHours, Assert.Single(document.Indicators).Interval);
    }

    [Fact]
    public void AVersionAboveTheSupportedRangeIsRefused()
    {
        var ex = Assert.Throws<RuleValidationException>(() => Parse(
            RuleDocument.SupportedVersion + 1,
            """{ "id": "ema", "type": "Ema", "params": { "period": 20 } }"""));

        Assert.Equal("$.version", ex.Path);
    }

    /// <summary>
    /// The important one. Property lookup is case-sensitive, so a tolerant indicator object
    /// would let this parse, ignore it, and return a confident wrong answer.
    /// </summary>
    [Theory]
    [InlineData("Interval")]
    [InlineData("timeframe")]
    [InlineData("tf")]
    public void AMisspeltIntervalKeyIsRefusedRatherThanIgnored(string key)
    {
        var ex = Assert.Throws<RuleValidationException>(() => Parse(
            2,
            $$"""{ "id": "adx", "type": "Adx", "{{key}}": "FourHours", "params": { "period": 14 } }"""));

        Assert.Equal($"$.indicators[0].{key}", ex.Path);
    }

    [Fact]
    public void TheDrillDownIntervalCannotBeReadByAnIndicator()
    {
        var ex = Assert.Throws<RuleValidationException>(() => Parse(
            2,
            """{ "id": "ema", "type": "Ema", "interval": "OneMinute", "params": { "period": 20 } }"""));

        Assert.Contains("drill-down", ex.Reason);
    }

    [Fact]
    public void AnUndefinedIntervalIsAValidationFailureNotAnEnumCrash()
    {
        // TryParse alone turns "99" into an undefined member, which then throws out of
        // Duration() with no path to point the user at.
        var ex = Assert.Throws<RuleValidationException>(() => Parse(
            2,
            """{ "id": "ema", "type": "Ema", "interval": "99", "params": { "period": 20 } }"""));

        Assert.Equal("$.indicators[0].interval", ex.Path);
    }

    [Fact]
    public void HighestReadsHighsAndLowestReadsLowsWhenNoSourceIsNamed()
    {
        var document = Parse(
            2,
            """
            { "id": "swingHigh", "type": "Highest", "params": { "period": 20 } },
            { "id": "swingLow", "type": "Lowest", "params": { "period": 20 } }
            """);

        Assert.Equal(PriceSource.High, document.Indicators[0].Source);
        Assert.Equal(PriceSource.Low, document.Indicators[1].Source);
    }

    [Fact]
    public void ANamedSourceStillWins()
    {
        var document = Parse(
            2,
            """{ "id": "swingHigh", "type": "Highest", "source": "Close", "params": { "period": 20 } }""");

        Assert.Equal(PriceSource.Close, Assert.Single(document.Indicators).Source);
    }

    private static RuleDocument Parse(int version, string indicators) =>
        RuleDocumentParser.Parse($$"""
            {
              "version": {{version}},
              "indicators": [ {{indicators}} ],
              "entry": { "long": { "op": "GreaterThan", "left": { "price": "Close" }, "right": 0 } },
              "stopLoss": { "kind": "Percent", "percent": 2 }
            }
            """);
}
