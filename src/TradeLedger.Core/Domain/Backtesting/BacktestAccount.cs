namespace TradeLedger.Core.Domain.Backtesting;

public sealed class BacktestAccount : IUserOwned
{
    private readonly List<BacktestRun> _runs = [];

    private BacktestAccount()
    {
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid UserId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public decimal StartingBalance { get; private set; }

    public string Currency { get; private set; } = "USDT";

    public BacktestAccountMode Mode { get; private set; } = BacktestAccountMode.Sequential;

    public Guid? BacktestStrategyId { get; private set; }

    public BacktestStrategy? BacktestStrategy { get; private set; }

    public bool IsActive { get; private set; } = true;

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    public IReadOnlyCollection<BacktestRun> Runs => _runs;

    public bool IsSequential => Mode == BacktestAccountMode.Sequential;

    public static BacktestAccount Create(
        string name,
        decimal startingBalance,
        BacktestAccountMode mode = BacktestAccountMode.Sequential,
        string? description = null,
        string? currency = null,
        Guid? strategyId = null,
        Guid userId = default) => new()
    {
        UserId = userId,
        Name = Guard.NotBlank(name, nameof(name)),
        StartingBalance = Guard.Positive(startingBalance, nameof(startingBalance)),
        Mode = mode,
        Description = description,
        Currency = string.IsNullOrWhiteSpace(currency) ? "USDT" : currency.Trim().ToUpperInvariant(),
        BacktestStrategyId = strategyId,
    };

    public void Rename(string name)
    {
        Name = Guard.NotBlank(name, nameof(name));
    }

    public void Describe(string? description)
    {
        Description = description;
    }

    public void BindStrategy(Guid? strategyId)
    {
        BacktestStrategyId = strategyId;
    }

    public void SwitchMode(BacktestAccountMode mode, bool hasRuns)
    {
        if (mode == Mode)
        {
            return;
        }

        if (hasRuns && mode == BacktestAccountMode.Sequential)
        {
            throw new ResourceConflictException(
                "cannot_switch_to_sequential",
                "Existing runs may overlap in time, and chaining them would compound the same market twice. Create a new account instead.");
        }

        Mode = mode;
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void Reactivate()
    {
        IsActive = true;
    }

    public void EnsureAcceptsNewRuns()
    {
        if (!IsActive)
        {
            throw new ResourceConflictException(
                "backtest_account_inactive",
                "This backtest account is archived and will not accept new runs.");
        }
    }
}
