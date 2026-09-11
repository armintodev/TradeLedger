namespace TradeLedger.Core.Domain;

public sealed class BalanceSnapshot : IUserOwned
{
    private BalanceSnapshot()
    {
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid UserId { get; private set; }

    public Guid AccountId { get; private set; }

    public Account? Account { get; private set; }

    public string Asset { get; private set; } = "USDT";

    public decimal WalletBalance { get; private set; }

    public decimal Available { get; private set; }

    public decimal Frozen { get; private set; }

    public decimal Margin { get; private set; }

    public decimal UnrealizedPnl { get; private set; }

    public decimal Equity { get; private set; }

    public decimal Bonus { get; private set; }

    public DateTimeOffset CapturedAt { get; private set; }

    public bool IsManual { get; private set; }

    public static BalanceSnapshot Capture(NewBalanceSnapshot spec)
    {
        var wallet = spec.WalletBalance;
        var unrealized = spec.UnrealizedPnl;

        return new BalanceSnapshot
        {
            UserId = spec.UserId,
            AccountId = Guard.NotEmpty(spec.AccountId, nameof(spec.AccountId)),
            Asset = string.IsNullOrWhiteSpace(spec.Asset) ? "USDT" : spec.Asset.ToUpperInvariant(),
            WalletBalance = wallet,
            Available = spec.Available ?? wallet,
            Frozen = spec.Frozen,
            Margin = spec.Margin,
            UnrealizedPnl = unrealized,
            Equity = wallet + unrealized,
            Bonus = spec.Bonus,
            CapturedAt = spec.CapturedAt ?? DateTimeOffset.UtcNow,
            IsManual = spec.IsManual,
        };
    }
}

public sealed record NewBalanceSnapshot
{
    public Guid UserId { get; init; }

    public required Guid AccountId { get; init; }

    public string? Asset { get; init; }

    public required decimal WalletBalance { get; init; }

    public decimal? Available { get; init; }

    public decimal Frozen { get; init; }

    public decimal Margin { get; init; }

    public decimal UnrealizedPnl { get; init; }

    public decimal Bonus { get; init; }

    public DateTimeOffset? CapturedAt { get; init; }

    public bool IsManual { get; init; }
}
