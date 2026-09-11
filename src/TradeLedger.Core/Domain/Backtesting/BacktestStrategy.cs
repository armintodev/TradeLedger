namespace TradeLedger.Core.Domain.Backtesting;

public sealed class BacktestStrategy : IUserOwned
{
    private BacktestStrategy()
    {
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid UserId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public string RuleJson { get; private set; } = string.Empty;

    public string RuleHash { get; private set; } = string.Empty;

    public int Version { get; private set; } = 1;

    public Guid? StrategyTermId { get; private set; }

    public TaxonomyTerm? StrategyTerm { get; private set; }

    public bool IsActive { get; private set; } = true;

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;

    public static BacktestStrategy Create(
        string name,
        string ruleJson,
        string ruleHash,
        string? description = null,
        Guid? strategyTermId = null,
        Guid userId = default) => new()
    {
        UserId = userId,
        Name = Guard.NotBlank(name, nameof(name)),
        RuleJson = Guard.NotBlank(ruleJson, nameof(ruleJson)),
        RuleHash = Guard.NotBlank(ruleHash, nameof(ruleHash)),
        Description = description,
        StrategyTermId = strategyTermId,
    };

    public void Revise(string ruleJson, string ruleHash)
    {
        Guard.NotBlank(ruleJson, nameof(ruleJson));
        Guard.NotBlank(ruleHash, nameof(ruleHash));

        if (string.Equals(ruleHash, RuleHash, StringComparison.Ordinal))
        {
            return;
        }

        RuleJson = ruleJson;
        RuleHash = ruleHash;
        Version++;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Rename(string name)
    {
        Name = Guard.NotBlank(name, nameof(name));
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Describe(string? description)
    {
        Description = description;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void BindTerm(Guid? strategyTermId)
    {
        StrategyTermId = strategyTermId;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Reactivate()
    {
        IsActive = true;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
