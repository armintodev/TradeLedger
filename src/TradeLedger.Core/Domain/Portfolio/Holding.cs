namespace TradeLedger.Core.Domain;

public sealed class Holding : IUserOwned
{
    private Holding()
    {
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid UserId { get; private set; }

    public Guid AccountId { get; private set; }

    public Account? Account { get; private set; }

    public HoldingKind Kind { get; private set; }

    public string Asset { get; private set; } = string.Empty;

    public decimal Quantity { get; private set; }

    public decimal? AverageEntryPrice { get; private set; }

    public decimal? EntryValueUsd { get; private set; }

    public decimal? CurrentPrice { get; private set; }

    public decimal? CurrentValueUsd { get; private set; }

    public DateTimeOffset? PricedAt { get; private set; }

    public string? PoolName { get; private set; }

    public decimal? FarmApr { get; private set; }

    public bool IsFarmed { get; private set; }

    public DateTimeOffset OpenedAt { get; private set; }

    public DateTimeOffset? ClosedAt { get; private set; }

    public string? Note { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    public bool IsOpen => ClosedAt is null;

    public decimal? UnrealizedProfitLoss =>
        CurrentValueUsd is { } current && EntryValueUsd is { } entry ? current - entry : null;

    public static Holding Open(NewHolding spec)
    {
        var holding = new Holding
        {
            UserId = spec.UserId,
            AccountId = Guard.NotEmpty(spec.AccountId, nameof(spec.AccountId)),
            Kind = spec.Kind,
            Asset = Guard.NotBlank(spec.Asset, nameof(spec.Asset)).ToUpperInvariant(),
            Quantity = Guard.Positive(spec.Quantity, nameof(spec.Quantity)),
            AverageEntryPrice = Guard.PositiveOrNull(spec.AverageEntryPrice, nameof(spec.AverageEntryPrice)),
            EntryValueUsd = spec.EntryValueUsd,
            PoolName = spec.PoolName,
            FarmApr = spec.FarmApr,
            IsFarmed = spec.IsFarmed,
            OpenedAt = Guard.NotDefault(spec.OpenedAt, nameof(spec.OpenedAt)),
            Note = spec.Note,
        };

        Guard.Rule(
            holding.Kind is HoldingKind.LiquidityPool or HoldingKind.Farm
                || string.IsNullOrWhiteSpace(holding.PoolName),
            "pool_on_non_pool_holding",
            "Only a liquidity pool or farm holding can name a pool.");

        holding.EntryValueUsd ??= holding.AverageEntryPrice * holding.Quantity;

        return holding;
    }

    public void Reprice(decimal price, DateTimeOffset pricedAt)
    {
        CurrentPrice = Guard.Positive(price, nameof(price));
        CurrentValueUsd = price * Quantity;
        PricedAt = pricedAt;
    }

    public void Resize(decimal quantity)
    {
        Quantity = Guard.Positive(quantity, nameof(quantity));

        if (CurrentPrice is { } price)
        {
            CurrentValueUsd = price * Quantity;
        }
    }

    public void Close(DateTimeOffset closedAt)
    {
        Guard.Rule(IsOpen, "holding_already_closed", "This holding is already closed.");

        Guard.Rule(
            closedAt >= OpenedAt,
            "closed_before_opened",
            "A holding cannot close before it was opened.");

        ClosedAt = closedAt;
    }

    public void Annotate(string? note)
    {
        Note = note;
    }
}

public sealed record NewHolding
{
    public Guid UserId { get; init; }

    public required Guid AccountId { get; init; }

    public required HoldingKind Kind { get; init; }

    public required string Asset { get; init; }

    public required decimal Quantity { get; init; }

    public decimal? AverageEntryPrice { get; init; }

    public decimal? EntryValueUsd { get; init; }

    public string? PoolName { get; init; }

    public decimal? FarmApr { get; init; }

    public bool IsFarmed { get; init; }

    public required DateTimeOffset OpenedAt { get; init; }

    public string? Note { get; init; }
}
