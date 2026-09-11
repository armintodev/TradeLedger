using TradeLedger.Core.Domain;

namespace TradeLedger.Api.Features.Portfolio;

public sealed record CreateHoldingRequest(
    Guid AccountId,
    HoldingKind Kind,
    string Asset,
    decimal Quantity,
    decimal? AverageEntryPrice,
    decimal? EntryValueUsd,
    string? PoolName,
    decimal? FarmApr,
    bool? IsFarmed,
    DateTimeOffset OpenedAt,
    string? Note)
{
    public NewHolding ToSpec() => new()
    {
        AccountId = AccountId,
        Kind = Kind,
        Asset = Asset,
        Quantity = Quantity,
        AverageEntryPrice = AverageEntryPrice,
        EntryValueUsd = EntryValueUsd,
        PoolName = PoolName,
        FarmApr = FarmApr,
        IsFarmed = IsFarmed ?? false,
        OpenedAt = OpenedAt,
        Note = Note,
    };
}

public sealed record CreateTransferRequest(
    Guid? FromAccountId,
    Guid? ToAccountId,
    TransferDirection Direction,
    string Asset,
    decimal Amount,
    decimal? Fee,
    decimal? ValueUsd,
    bool? WriteOff,
    string? Network,
    string? TxHash,
    string? Counterparty,
    string? Note,
    DateTimeOffset OccurredAt)
{
    public NewTransfer ToSpec() => new()
    {
        FromAccountId = FromAccountId,
        ToAccountId = ToAccountId,
        Direction = Direction,
        Asset = Asset,
        Amount = Amount,
        Fee = Fee ?? 0m,
        ValueUsd = ValueUsd,
        WriteOff = WriteOff ?? false,
        Network = Network,
        TxHash = TxHash,
        Counterparty = Counterparty,
        Note = Note,
        OccurredAt = OccurredAt,
    };
}

public sealed record CreateSnapshotRequest(
    Guid AccountId,
    string? Asset,
    decimal WalletBalance,
    decimal? Available,
    decimal? UnrealizedPnl,
    DateTimeOffset? CapturedAt)
{
    public NewBalanceSnapshot ToSpec() => new()
    {
        AccountId = AccountId,
        Asset = Asset,
        WalletBalance = WalletBalance,
        Available = Available,
        UnrealizedPnl = UnrealizedPnl ?? 0m,
        CapturedAt = CapturedAt,
        IsManual = true,
    };
}

public sealed record CloseHoldingRequest(DateTimeOffset? ClosedAt);

public sealed record RepriceHoldingRequest(decimal Price, DateTimeOffset? PricedAt);
