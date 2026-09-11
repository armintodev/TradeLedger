using TradeLedger.Core.Backtesting.Engine;
using TradeLedger.Core.Domain;

namespace TradeLedger.UnitTests.Backtesting;

public class CostAndLiquidationTests
{
    private static readonly CostModel Costs = new(
        TakerFeeRate: 0.0006m,
        MakerFeeRate: 0.0002m,
        SlippageRate: 0.0005m);

    [Fact]
    public void SlippageMakesALongEntryMoreExpensive()
    {
        Assert.True(Costs.EntryFillPrice(TradeSide.Long, 100m) > 100m);
    }

    [Fact]
    public void SlippageMakesAShortEntryCheaper()
    {
        Assert.True(Costs.EntryFillPrice(TradeSide.Short, 100m) < 100m);
    }

    [Fact]
    public void ALongStopFillsBelowTheStopPrice()
    {
        Assert.True(Costs.StopFillPrice(TradeSide.Long, 98m) < 98m);
    }

    [Fact]
    public void AShortStopFillsAboveTheStopPrice()
    {
        Assert.True(Costs.StopFillPrice(TradeSide.Short, 102m) > 102m);
    }

    [Fact]
    public void TakeProfitNeverReceivesFavourableSlippage()
    {
        Assert.Equal(104m, Costs.TakeProfitFillPrice(104m));
    }

    [Fact]
    public void SlippageAlwaysMovesAgainstTheTrader()
    {
        var longEntry = Costs.EntryFillPrice(TradeSide.Long, 100m);
        var longStop = Costs.StopFillPrice(TradeSide.Long, 98m);

        var worseEntry = longEntry - 100m;
        var worseExit = 98m - longStop;

        Assert.True(worseEntry > 0);
        Assert.True(worseExit > 0);
    }

    [Fact]
    public void ZeroSlippageLeavesPricesUntouched()
    {
        var frictionless = new CostModel(0.0006m, 0.0002m, 0m);

        Assert.Equal(100m, frictionless.EntryFillPrice(TradeSide.Long, 100m));
        Assert.Equal(98m, frictionless.StopFillPrice(TradeSide.Long, 98m));
    }

    [Fact]
    public void TakerFeeIsChargedOnNotionalRegardlessOfSign()
    {
        Assert.Equal(6m, Costs.TakerFee(10_000m));
        Assert.Equal(6m, Costs.TakerFee(-10_000m));
    }

    [Fact]
    public void MakerFeeIsCheaperThanTakerFee()
    {
        Assert.True(Costs.MakerFee(10_000m) < Costs.TakerFee(10_000m));
    }

    [Fact]
    public void ALongPaysFundingWhenTheRateIsPositive()
    {
        var payment = CostModel.FundingPayment(TradeSide.Long, 10_000m, 0.0001m);

        Assert.True(payment < 0);
        Assert.Equal(-1m, payment);
    }

    [Fact]
    public void AShortReceivesFundingWhenTheRateIsPositive()
    {
        Assert.Equal(1m, CostModel.FundingPayment(TradeSide.Short, 10_000m, 0.0001m));
    }

    [Fact]
    public void ALongReceivesFundingWhenTheRateIsNegative()
    {
        Assert.True(CostModel.FundingPayment(TradeSide.Long, 10_000m, -0.0001m) > 0);
    }

    [Fact]
    public void GrossProfitLossFollowsTheDirectionOfTheTrade()
    {
        Assert.Equal(100m, CostModel.GrossProfitLoss(TradeSide.Long, 100m, 110m, 10m));
        Assert.Equal(-100m, CostModel.GrossProfitLoss(TradeSide.Long, 100m, 90m, 10m));
        Assert.Equal(100m, CostModel.GrossProfitLoss(TradeSide.Short, 100m, 90m, 10m));
        Assert.Equal(-100m, CostModel.GrossProfitLoss(TradeSide.Short, 100m, 110m, 10m));
    }

    [Fact]
    public void LongLiquidationSitsBelowEntryByTheMarginBuffer()
    {
        var price = LiquidationModel.LiquidationPrice(TradeSide.Long, 100m, 10, 0.005m);

        Assert.Equal(90.5m, price);
    }

    [Fact]
    public void ShortLiquidationSitsAboveEntryByTheMarginBuffer()
    {
        var price = LiquidationModel.LiquidationPrice(TradeSide.Short, 100m, 10, 0.005m);

        Assert.Equal(109.5m, price);
    }

    [Fact]
    public void HigherLeverageDragsLiquidationTowardsEntry()
    {
        var low = LiquidationModel.LiquidationPrice(TradeSide.Long, 100m, 2, 0.005m);
        var high = LiquidationModel.LiquidationPrice(TradeSide.Long, 100m, 50, 0.005m);

        Assert.True(high > low);
        Assert.True(high < 100m);
    }

    [Fact]
    public void SingleLeverageCannotLiquidateAboveZero()
    {
        var price = LiquidationModel.LiquidationPrice(TradeSide.Long, 100m, 1, 0.005m);

        Assert.Equal(0.5m, price);
    }

    [Fact]
    public void LiquidationPriceIsNeverNegative()
    {
        var price = LiquidationModel.LiquidationPrice(TradeSide.Long, 100m, 1, 0m);

        Assert.True(price >= 0m);
    }

    [Fact]
    public void LeverageBelowOneIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => LiquidationModel.LiquidationPrice(TradeSide.Long, 100m, 0, 0.005m));
    }

    [Fact]
    public void ATightStopAtModestLeverageIsHitLongBeforeLiquidation()
    {
        var liquidation = LiquidationModel.LiquidationPrice(TradeSide.Long, 100m, 5, 0.005m);

        Assert.False(
            LiquidationModel.WouldLiquidateBeforeStop(TradeSide.Long, 100m, 98m, liquidation));
    }

    [Fact]
    public void AWideStopAtHighLeverageLiquidatesFirst()
    {
        var liquidation = LiquidationModel.LiquidationPrice(TradeSide.Long, 100m, 25, 0.005m);

        Assert.True(
            LiquidationModel.WouldLiquidateBeforeStop(TradeSide.Long, 100m, 90m, liquidation));
    }

    [Fact]
    public void TheSameCheckWorksInvertedForShorts()
    {
        var liquidation = LiquidationModel.LiquidationPrice(TradeSide.Short, 100m, 25, 0.005m);

        Assert.True(
            LiquidationModel.WouldLiquidateBeforeStop(TradeSide.Short, 100m, 110m, liquidation));

        Assert.False(
            LiquidationModel.WouldLiquidateBeforeStop(TradeSide.Short, 100m, 102m, liquidation));
    }

    [Fact]
    public void AFullRoundTripNetsGrossMinusFeesPlusFunding()
    {
        const decimal quantity = 100m;
        var entry = Costs.EntryFillPrice(TradeSide.Long, 100m);
        var exit = Costs.TakeProfitFillPrice(104m);

        var gross = CostModel.GrossProfitLoss(TradeSide.Long, entry, exit, quantity);
        var fees = Costs.TakerFee(entry * quantity) + Costs.TakerFee(exit * quantity);
        var funding = CostModel.FundingPayment(TradeSide.Long, entry * quantity, 0.0001m);

        var net = gross - fees + funding;

        Assert.True(net < gross);
        Assert.True(net > 0);
    }
}
