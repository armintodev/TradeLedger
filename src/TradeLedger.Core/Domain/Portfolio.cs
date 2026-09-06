namespace TradeLedger.Core.Domain;

public sealed class FundingPayment : IUserOwned
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }
    public Guid AccountId { get; set; }
    public Account? Account { get; set; }

    public Guid? TradeId { get; set; }
    public Trade? Trade { get; set; }

    public required string Symbol { get; set; }

    public decimal Amount { get; set; }

    public string Asset { get; set; } = "USDT";
    public decimal? FundingRate { get; set; }

    public DateTimeOffset OccurredAt { get; set; }
    public long? OccurredAtRawMs { get; set; }
    public string? ExchangeFundingId { get; set; }
}

public sealed class Transfer : IUserOwned
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }

    public Guid? FromAccountId { get; set; }
    public Account? FromAccount { get; set; }

    public Guid? ToAccountId { get; set; }
    public Account? ToAccount { get; set; }

    public TransferDirection Direction { get; set; }

    public required string Asset { get; set; }
    public decimal Amount { get; set; }
    public decimal Fee { get; set; }

    public decimal? ValueUsd { get; set; }

    public bool WriteOff { get; set; }

    public string? Network { get; set; }
    public string? TxHash { get; set; }
    public string? Counterparty { get; set; }
    public string? Note { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public ICollection<Attachment> Attachments { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Holding : IUserOwned
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }
    public Guid AccountId { get; set; }
    public Account? Account { get; set; }

    public HoldingKind Kind { get; set; }

    public required string Asset { get; set; }

    public decimal Quantity { get; set; }
    public decimal? AverageEntryPrice { get; set; }
    public decimal? EntryValueUsd { get; set; }

    public decimal? CurrentPrice { get; set; }

    public decimal? CurrentValueUsd { get; set; }
    public DateTimeOffset? PricedAt { get; set; }

    public string? PoolName { get; set; }

    public decimal? FarmApr { get; set; }

    public bool IsFarmed { get; set; }

    public DateTimeOffset OpenedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public string? Note { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class BalanceSnapshot : IUserOwned
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }
    public Guid AccountId { get; set; }
    public Account? Account { get; set; }

    public string Asset { get; set; } = "USDT";

    public decimal WalletBalance { get; set; }

    public decimal Available { get; set; }

    public decimal Frozen { get; set; }

    public decimal Margin { get; set; }

    public decimal UnrealizedPnl { get; set; }

    public decimal Equity { get; set; }

    public decimal Bonus { get; set; }

    public DateTimeOffset CapturedAt { get; set; }
    public bool IsManual { get; set; }
}

public sealed class Attachment : IUserOwned
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }

    public Guid? TradeId { get; set; }
    public Trade? Trade { get; set; }

    public Guid? TransferId { get; set; }
    public Transfer? Transfer { get; set; }

    public required string Slot { get; set; }

    public required string StorageKey { get; set; }
    public required string FileName { get; set; }
    public required string ContentType { get; set; }
    public long SizeBytes { get; set; }
    public string? Caption { get; set; }

    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;
}
