using TradeLedger.Core.Analytics;

namespace TradeLedger.UnitTests;

public class PositionSizeCalculatorTests
{
    private readonly PositionSizeCalculator _calculator = new();

    [Fact]
    public void Calculate_SizesSoAStopOutLosesExactlyTheRiskBudget()
    {
        var result = _calculator.Calculate(new PositionSizeRequest
        {
            Balance = 10_000m,
            EntryPrice = 100m,
            StopLossPrice = 95m,
            RiskFraction = 0.01m,
            RiskReward = 2m,
        });

        Assert.Equal(100m, result.RiskAmount);

        Assert.Equal(20m, result.Quantity);
        Assert.Equal(2_000m, result.OrderValue);
        Assert.Equal(100m, result.EstimatedLoss);
    }

    [Fact]
    public void Calculate_TakeProfitSitsAtTheRequestedRMultiple()
    {
        var result = _calculator.Calculate(new PositionSizeRequest
        {
            Balance = 10_000m,
            EntryPrice = 100m,
            StopLossPrice = 95m,
            RiskFraction = 0.01m,
            RiskReward = 3m,
        });

        Assert.Equal(115m, result.TakeProfitPrice);
        Assert.Equal("Long", result.Side);
    }

    [Fact]
    public void Calculate_InfersShortWhenStopIsAboveEntry()
    {
        var result = _calculator.Calculate(new PositionSizeRequest
        {
            Balance = 10_000m,
            EntryPrice = 100m,
            StopLossPrice = 105m,
            RiskFraction = 0.01m,
            RiskReward = 2m,
        });

        Assert.Equal("Short", result.Side);
        Assert.Equal(90m, result.TakeProfitPrice);
    }

    [Fact]
    public void Calculate_LeverageReducesMarginNotPositionSize()
    {
        var unlevered = _calculator.Calculate(Request(leverage: 1));
        var levered = _calculator.Calculate(Request(leverage: 10));

        Assert.Equal(unlevered.Quantity, levered.Quantity);
        Assert.Equal(unlevered.OrderValue, levered.OrderValue);

        Assert.Equal(unlevered.Margin / 10m, levered.Margin);
    }

    [Fact]
    public void Calculate_FeesReduceSizeSoTheBudgetStillHolds()
    {
        var free = _calculator.Calculate(Request(feeRate: null));
        var withFees = _calculator.Calculate(Request(feeRate: 0.0006m));

        Assert.True(withFees.Quantity < free.Quantity);
        Assert.True(withFees.FeesEntryPlusStop > 0m);

        Assert.Equal(100m, Math.Round(withFees.EstimatedLoss, 6));
    }

    [Fact]
    public void Calculate_StopToEntryRatioIsTheFractionalDistance()
    {
        var result = _calculator.Calculate(Request());

        Assert.Equal(0.05m, result.StopToEntryRatio);
    }

    [Fact]
    public void Calculate_RejectsStopEqualToEntry()
    {
        var request = new PositionSizeRequest
        {
            Balance = 10_000m,
            EntryPrice = 100m,
            StopLossPrice = 100m,
            RiskFraction = 0.01m,
        };

        Assert.Throws<ArgumentException>(() => _calculator.Calculate(request));
    }

    [Fact]
    public void Calculate_RejectsNonPositivePrices()
    {
        Assert.Throws<ArgumentException>(() => _calculator.Calculate(new PositionSizeRequest
        {
            Balance = 10_000m,
            EntryPrice = 0m,
            StopLossPrice = 95m,
            RiskFraction = 0.01m,
        }));

        Assert.Throws<ArgumentException>(() => _calculator.Calculate(new PositionSizeRequest
        {
            Balance = 10_000m,
            EntryPrice = 100m,
            StopLossPrice = 0m,
            RiskFraction = 0.01m,
        }));
    }

    private static PositionSizeRequest Request(int leverage = 1, decimal? feeRate = null) => new()
    {
        Balance = 10_000m,
        EntryPrice = 100m,
        StopLossPrice = 95m,
        RiskFraction = 0.01m,
        RiskReward = 2m,
        Leverage = leverage,
        AverageFeeRate = feeRate,
    };
}
