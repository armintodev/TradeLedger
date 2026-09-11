namespace TradeLedger.Core.Domain;

public sealed class Account : IUserOwned
{
    private readonly List<Trade> _trades = [];
    private readonly List<BalanceSnapshot> _balanceSnapshots = [];
    private readonly List<Holding> _holdings = [];

    private Account()
    {
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid UserId { get; private set; }

    public AppUser? User { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public AccountKind Kind { get; private set; }

    public Venue Venue { get; private set; } = Venue.Manual;

    public SyncMode SyncMode { get; private set; } = SyncMode.Manual;

    public string QuoteAsset { get; private set; } = "USDT";

    public bool IsActive { get; private set; } = true;

    public DateTimeOffset? TrackedFrom { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    public ExchangeCredential? Credential { get; private set; }

    public IReadOnlyCollection<Trade> Trades => _trades;

    public IReadOnlyCollection<BalanceSnapshot> BalanceSnapshots => _balanceSnapshots;

    public IReadOnlyCollection<Holding> Holdings => _holdings;

    public bool IsExchangeAccount => Venue != Venue.Manual;

    public bool SyncsAutomatically =>
        IsActive && SyncMode == SyncMode.Api && Credential is { IsEnabled: true };

    public static Account Create(
        string name,
        AccountKind kind,
        Venue venue,
        SyncMode syncMode,
        string? quoteAsset = null,
        DateTimeOffset? trackedFrom = null,
        Guid userId = default) => new()
    {
        UserId = userId,
        Name = Guard.NotBlank(name, nameof(name)),
        Kind = kind,
        Venue = venue,
        SyncMode = venue == Venue.Manual ? SyncMode.Manual : syncMode,
        QuoteAsset = string.IsNullOrWhiteSpace(quoteAsset) ? "USDT" : quoteAsset.Trim().ToUpperInvariant(),
        TrackedFrom = trackedFrom,
    };

    public void Rename(string name)
    {
        Name = Guard.NotBlank(name, nameof(name));
    }

    public void TrackFrom(DateTimeOffset? trackedFrom)
    {
        TrackedFrom = trackedFrom;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void Reactivate()
    {
        IsActive = true;
    }

    public ExchangeCredential AttachCredential(
        byte[] apiKeyCipher,
        byte[] apiSecretCipher,
        string apiKeyHint,
        string? label,
        string? verifiedViaEgress)
    {
        Guard.Rule(
            IsExchangeAccount,
            "credential_on_manual_account",
            "A manual account has no exchange to authenticate against.");

        if (Credential is null)
        {
            Credential = ExchangeCredential.For(this, apiKeyCipher, apiSecretCipher, apiKeyHint, label);
        }
        else
        {
            Credential.Rotate(apiKeyCipher, apiSecretCipher, apiKeyHint, label);
        }

        Credential.MarkVerified(verifiedViaEgress);

        SyncMode = SyncMode.Api;

        return Credential;
    }

    public void DetachCredential()
    {
        Credential = null;
        SyncMode = SyncMode.Manual;
    }
}
