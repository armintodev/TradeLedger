using Microsoft.Extensions.Options;
using TradeLedger.Core.Backtesting;

namespace TradeLedger.UnitTests.Backtesting;

public class BacktestOptionsValidatorTests
{
    [Fact]
    public void TheShippedDefaultsAreValid()
    {
        Assert.True(Validate(new BacktestOptions()).Succeeded);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void RiskInsideTheOneToFiveBandIsAccepted(int risk)
    {
        Assert.True(Validate(new BacktestOptions { RiskPercentPerPosition = risk }).Succeeded);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.9)]
    [InlineData(5.1)]
    [InlineData(10)]
    [InlineData(-2)]
    public void RiskOutsideTheBandFailsStartupRatherThanBeingClamped(decimal risk)
    {
        var result = Validate(new BacktestOptions { RiskPercentPerPosition = risk });

        Assert.True(result.Failed);
        Assert.Contains("RiskPercentPerPosition", result.FailureMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(1.99)]
    public void ARiskRewardRatioBelowTwoIsRejected(decimal ratio)
    {
        var result = Validate(new BacktestOptions { RiskRewardRatio = ratio });

        Assert.True(result.Failed);
        Assert.Contains("RiskRewardRatio", result.FailureMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(2.5)]
    [InlineData(4)]
    public void ARiskRewardRatioOfTwoOrMoreIsAccepted(decimal ratio)
    {
        Assert.True(Validate(new BacktestOptions { RiskRewardRatio = ratio }).Succeeded);
    }

    [Fact]
    public void LeverageAboveTheCeilingIsRejected()
    {
        var result = Validate(new BacktestOptions { DefaultLeverage = 50, MaxLeverage = 25 });

        Assert.True(result.Failed);
        Assert.Contains("DefaultLeverage", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void LeverageBelowOneIsRejected()
    {
        Assert.True(Validate(new BacktestOptions { DefaultLeverage = 0 }).Failed);
    }

    [Fact]
    public void NegativeCostsAreRejected()
    {
        Assert.True(Validate(new BacktestOptions { DefaultTakerFeeRate = -0.001m }).Failed);
        Assert.True(Validate(new BacktestOptions { DefaultSlippageRate = -0.001m }).Failed);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1)]
    [InlineData(1.5)]
    public void AnImpossibleMaintenanceMarginRateIsRejected(decimal rate)
    {
        Assert.True(Validate(new BacktestOptions { DefaultMaintenanceMarginRate = rate }).Failed);
    }

    [Fact]
    public void EngineVersionMustBeAtLeastOneSoResultsStayExplainable()
    {
        Assert.True(Validate(new BacktestOptions { EngineVersion = 0 }).Failed);
    }

    [Fact]
    public void EveryBrokenSettingIsReportedNotJustTheFirst()
    {
        var result = Validate(new BacktestOptions
        {
            RiskPercentPerPosition = 99m,
            RiskRewardRatio = 1m,
            EngineVersion = 0,
        });

        Assert.True(result.Failed);
        Assert.Contains("RiskPercentPerPosition", result.FailureMessage, StringComparison.Ordinal);
        Assert.Contains("RiskRewardRatio", result.FailureMessage, StringComparison.Ordinal);
        Assert.Contains("EngineVersion", result.FailureMessage, StringComparison.Ordinal);
    }

    private static ValidateOptionsResult Validate(BacktestOptions options) =>
        new BacktestOptionsValidator().Validate(null, options);
}
