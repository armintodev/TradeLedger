namespace TradeLedger.Core.Domain;

public sealed class Account : IUserOwned
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }
    public AppUser? User { get; set; }

    public required string Name { get; set; }
    public AccountKind Kind { get; set; }
    public Venue Venue { get; set; } = Venue.Manual;
    public SyncMode SyncMode { get; set; } = SyncMode.Manual;

    public string QuoteAsset { get; set; } = "USDT";

    public bool IsActive { get; set; } = true;

    public DateTimeOffset? TrackedFrom { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ExchangeCredential? Credential { get; set; }
    public ICollection<Trade> Trades { get; set; } = [];
    public ICollection<BalanceSnapshot> BalanceSnapshots { get; set; } = [];
    public ICollection<Holding> Holdings { get; set; } = [];
}
