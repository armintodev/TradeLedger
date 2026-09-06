using TradeLedger.Core.Domain;

namespace TradeLedger.Api.Features.Portfolio;

public sealed record HoldingResponse(
    Guid Id,
    Guid AccountId,
    HoldingKind Kind,
    string Asset,
    decimal Quantity,
    decimal? AverageEntryPrice,
    decimal? EntryValueUsd,
    decimal? CurrentPrice,
    decimal? CurrentValueUsd,
    DateTimeOffset? PricedAt,
    string? PoolName,
    decimal? FarmApr,
    bool IsFarmed,
    DateTimeOffset OpenedAt,
    DateTimeOffset? ClosedAt,
    string? Note)
{
    public static HoldingResponse From(Holding h) => new(
        h.Id, h.AccountId, h.Kind, h.Asset, h.Quantity, h.AverageEntryPrice, h.EntryValueUsd,
        h.CurrentPrice, h.CurrentValueUsd, h.PricedAt, h.PoolName, h.FarmApr, h.IsFarmed,
        h.OpenedAt, h.ClosedAt, h.Note);
}

public sealed record TransferResponse(
    Guid Id,
    Guid? FromAccountId,
    Guid? ToAccountId,
    TransferDirection Direction,
    string Asset,
    decimal Amount,
    decimal Fee,
    decimal? ValueUsd,
    bool WriteOff,
    string? Network,
    string? TxHash,
    string? Counterparty,
    string? Note,
    DateTimeOffset OccurredAt)
{
    public static TransferResponse From(Transfer t) => new(
        t.Id, t.FromAccountId, t.ToAccountId, t.Direction, t.Asset, t.Amount, t.Fee,
        t.ValueUsd, t.WriteOff, t.Network, t.TxHash, t.Counterparty, t.Note, t.OccurredAt);
}

public sealed record BalanceSnapshotResponse(
    Guid Id,
    Guid AccountId,
    string Asset,
    decimal WalletBalance,
    decimal Available,
    decimal Frozen,
    decimal Margin,
    decimal UnrealizedPnl,
    decimal Equity,
    decimal Bonus,
    DateTimeOffset CapturedAt,
    bool IsManual)
{
    public static BalanceSnapshotResponse From(BalanceSnapshot s) => new(
        s.Id, s.AccountId, s.Asset, s.WalletBalance, s.Available, s.Frozen, s.Margin,
        s.UnrealizedPnl, s.Equity, s.Bonus, s.CapturedAt, s.IsManual);
}
