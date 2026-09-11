using Microsoft.Extensions.Options;

namespace TradeLedger.Core.Backtesting;

public sealed class BacktestOptions
{
    public const string SectionName = "Backtest";

    public const decimal MinRiskPercent = 1m;
    public const decimal MaxRiskPercent = 5m;
    public const decimal MinRiskRewardRatio = 2m;

    public int EngineVersion { get; set; } = 1;

    public decimal RiskPercentPerPosition { get; set; } = 2m;

    public decimal RiskRewardRatio { get; set; } = 2m;

    public int DefaultLeverage { get; set; } = 5;

    public int MaxLeverage { get; set; } = 25;

    public int MaxConcurrentRunsPerUser { get; set; } = 1;

    public TimeSpan QueuePollInterval { get; set; } = TimeSpan.FromSeconds(2);

    public TimeSpan StaleRunTimeout { get; set; } = TimeSpan.FromHours(2);

    public int MaxBarsPerRun { get; set; } = 2_000_000;

    public decimal DefaultTakerFeeRate { get; set; } = 0.0006m;

    public decimal DefaultMakerFeeRate { get; set; } = 0.0002m;

    public decimal DefaultSlippageRate { get; set; } = 0.0005m;

    public decimal DefaultMaintenanceMarginRate { get; set; } = 0.005m;
}

public sealed class BacktestOptionsValidator : IValidateOptions<BacktestOptions>
{
    public ValidateOptionsResult Validate(string? name, BacktestOptions options)
    {
        var failures = new List<string>();

        if (options.RiskPercentPerPosition < BacktestOptions.MinRiskPercent
            || options.RiskPercentPerPosition > BacktestOptions.MaxRiskPercent)
        {
            failures.Add(
                $"Backtest:RiskPercentPerPosition is {options.RiskPercentPerPosition} but must be " +
                $"between {BacktestOptions.MinRiskPercent} and {BacktestOptions.MaxRiskPercent}.");
        }

        if (options.RiskRewardRatio < BacktestOptions.MinRiskRewardRatio)
        {
            failures.Add(
                $"Backtest:RiskRewardRatio is {options.RiskRewardRatio} but must be at least " +
                $"{BacktestOptions.MinRiskRewardRatio}.");
        }

        if (options.MaxLeverage < 1)
        {
            failures.Add($"Backtest:MaxLeverage is {options.MaxLeverage} but must be at least 1.");
        }

        if (options.DefaultLeverage < 1 || options.DefaultLeverage > options.MaxLeverage)
        {
            failures.Add(
                $"Backtest:DefaultLeverage is {options.DefaultLeverage} but must be between 1 and " +
                $"Backtest:MaxLeverage ({options.MaxLeverage}).");
        }

        if (options.DefaultTakerFeeRate < 0 || options.DefaultMakerFeeRate < 0)
        {
            failures.Add("Backtest fee rates must not be negative.");
        }

        if (options.DefaultSlippageRate < 0)
        {
            failures.Add("Backtest:DefaultSlippageRate must not be negative.");
        }

        if (options.DefaultMaintenanceMarginRate is < 0 or >= 1)
        {
            failures.Add(
                "Backtest:DefaultMaintenanceMarginRate must be at least 0 and below 1.");
        }

        if (options.EngineVersion < 1)
        {
            failures.Add("Backtest:EngineVersion must be at least 1.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
