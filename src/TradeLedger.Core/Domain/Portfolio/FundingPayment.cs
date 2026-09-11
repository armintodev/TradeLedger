namespace TradeLedger.Core.Domain;

public sealed class FundingPayment : IUserOwned
{
    private FundingPayment()
    {
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid UserId { get; private set; }

    public Guid AccountId { get; private set; }

    public Account? Account { get; private set; }

    public Guid? TradeId { get; private set; }

    public Trade? Trade { get; private set; }

    public string Symbol { get; private set; } = string.Empty;

    public decimal Amount { get; private set; }

    public string Asset { get; private set; } = "USDT";

    public decimal? FundingRate { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public long? OccurredAtRawMs { get; private set; }

    public string? ExchangeFundingId { get; private set; }

    public bool WasPaid => Amount < 0m;

    public static FundingPayment Record(
        Guid accountId,
        string symbol,
        decimal amount,
        DateTimeOffset occurredAt,
        decimal? fundingRate = null,
        string? asset = null,
        long? occurredAtRawMs = null,
        string? exchangeFundingId = null,
        Guid userId = default) => new()
    {
        UserId = userId,
        AccountId = Guard.NotEmpty(accountId, nameof(accountId)),
        Symbol = Guard.NotBlank(symbol, nameof(symbol)),
        Amount = amount,
        Asset = string.IsNullOrWhiteSpace(asset) ? "USDT" : asset.ToUpperInvariant(),
        FundingRate = fundingRate,
        OccurredAt = occurredAt,
        OccurredAtRawMs = occurredAtRawMs,
        ExchangeFundingId = exchangeFundingId,
    };

    public void AttributeTo(Trade trade)
    {
        Guard.Rule(
            trade.AccountId == AccountId,
            "funding_account_mismatch",
            "Funding can only be attributed to a trade on the same account.");

        TradeId = trade.Id;
    }
}

public sealed class Attachment : IUserOwned
{
    private Attachment()
    {
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid UserId { get; private set; }

    public Guid? TradeId { get; private set; }

    public Trade? Trade { get; private set; }

    public Guid? TransferId { get; private set; }

    public Transfer? Transfer { get; private set; }

    public string Slot { get; private set; } = string.Empty;

    public string StorageKey { get; private set; } = string.Empty;

    public string FileName { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public long SizeBytes { get; private set; }

    public string? Caption { get; private set; }

    public DateTimeOffset UploadedAt { get; private set; } = DateTimeOffset.UtcNow;

    public static Attachment ForTrade(
        Trade trade,
        string slot,
        string storageKey,
        string fileName,
        string contentType,
        long sizeBytes,
        string? caption = null) => new()
    {
        UserId = trade.UserId,
        TradeId = trade.Id,
        Slot = Guard.NotBlank(slot, nameof(slot)),
        StorageKey = Guard.NotBlank(storageKey, nameof(storageKey)),
        FileName = Guard.NotBlank(fileName, nameof(fileName)),
        ContentType = Guard.NotBlank(contentType, nameof(contentType)),
        SizeBytes = sizeBytes,
        Caption = caption,
    };

    public static Attachment ForTransfer(
        Transfer transfer,
        string slot,
        string storageKey,
        string fileName,
        string contentType,
        long sizeBytes,
        string? caption = null) => new()
    {
        UserId = transfer.UserId,
        TransferId = transfer.Id,
        Slot = Guard.NotBlank(slot, nameof(slot)),
        StorageKey = Guard.NotBlank(storageKey, nameof(storageKey)),
        FileName = Guard.NotBlank(fileName, nameof(fileName)),
        ContentType = Guard.NotBlank(contentType, nameof(contentType)),
        SizeBytes = sizeBytes,
        Caption = caption,
    };

    public void Recaption(string? caption)
    {
        Caption = caption;
    }
}
