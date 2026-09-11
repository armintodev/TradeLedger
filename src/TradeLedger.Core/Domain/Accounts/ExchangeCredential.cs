namespace TradeLedger.Core.Domain;

public sealed class ExchangeCredential : IUserOwned
{
    private ExchangeCredential()
    {
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid UserId { get; private set; }

    public Guid AccountId { get; private set; }

    public Account? Account { get; private set; }

    public Venue Venue { get; private set; } = Venue.Bitunix;

    public byte[] ApiKeyCipher { get; private set; } = [];

    public byte[] ApiSecretCipher { get; private set; } = [];

    public string ApiKeyHint { get; private set; } = string.Empty;

    public string? Label { get; private set; }

    public bool IsEnabled { get; private set; } = true;

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? LastVerifiedAt { get; private set; }

    public string? LastVerificationError { get; private set; }

    public string? VerifiedViaEgress { get; private set; }

    public bool IsVerified => LastVerifiedAt is not null && LastVerificationError is null;

    public static ExchangeCredential For(
        Account account,
        byte[] apiKeyCipher,
        byte[] apiSecretCipher,
        string apiKeyHint,
        string? label = null) => new()
    {
        UserId = account.UserId,
        AccountId = account.Id,
        Venue = account.Venue,
        ApiKeyCipher = NotEmpty(apiKeyCipher, nameof(apiKeyCipher)),
        ApiSecretCipher = NotEmpty(apiSecretCipher, nameof(apiSecretCipher)),
        ApiKeyHint = Guard.NotBlank(apiKeyHint, nameof(apiKeyHint)),
        Label = label,
        IsEnabled = true,
    };

    public void Rotate(byte[] apiKeyCipher, byte[] apiSecretCipher, string apiKeyHint, string? label)
    {
        ApiKeyCipher = NotEmpty(apiKeyCipher, nameof(apiKeyCipher));
        ApiSecretCipher = NotEmpty(apiSecretCipher, nameof(apiSecretCipher));
        ApiKeyHint = Guard.NotBlank(apiKeyHint, nameof(apiKeyHint));
        Label = label;
        IsEnabled = true;
    }

    public void MarkVerified(string? viaEgress)
    {
        LastVerifiedAt = DateTimeOffset.UtcNow;
        LastVerificationError = null;
        VerifiedViaEgress = viaEgress;
    }

    public void MarkVerificationFailed(string error)
    {
        LastVerificationError = error;
    }

    public void Disable()
    {
        IsEnabled = false;
    }

    public void Enable()
    {
        IsEnabled = true;
    }

    private static byte[] NotEmpty(byte[] value, string field) =>
        value is { Length: > 0 }
            ? value
            : throw new DomainValidationException(field, $"{field} is required.");
}
