namespace TradeLedger.Core.Domain;

public sealed class ExchangeCredential : IUserOwned
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }
    public Guid AccountId { get; set; }
    public Account? Account { get; set; }

    public Venue Venue { get; set; } = Venue.Bitunix;

    public required byte[] ApiKeyCipher { get; set; }
    public required byte[] ApiSecretCipher { get; set; }

    public required string ApiKeyHint { get; set; }

    public string? Label { get; set; }
    public bool IsEnabled { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastVerifiedAt { get; set; }
    public string? LastVerificationError { get; set; }
}
