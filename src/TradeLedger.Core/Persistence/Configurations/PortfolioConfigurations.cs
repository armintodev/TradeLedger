using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradeLedger.Core.Domain;
using TradeLedger.Core.Shared;

namespace TradeLedger.Core.Persistence.Configurations;

public sealed class FundingPaymentConfiguration : IEntityTypeConfiguration<FundingPayment>
{
    public void Configure(EntityTypeBuilder<FundingPayment> b)
    {
        b.ToTable("funding_payments");
        b.HasKey(f => f.Id);

        b.Property(f => f.Symbol).HasMaxLength(32).IsRequired();
        b.Property(f => f.Asset).HasMaxLength(16);
        b.Property(f => f.Amount).HasColumnType(Precision.Money);
        b.Property(f => f.FundingRate).HasColumnType(Precision.Ratio);
        b.Property(f => f.ExchangeFundingId).HasMaxLength(64);

        b.HasOne(f => f.Account).WithMany()
            .HasForeignKey(f => f.AccountId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(f => f.Trade).WithMany(t => t.FundingPayments)
            .HasForeignKey(f => f.TradeId).OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(f => new { f.AccountId, f.ExchangeFundingId })
            .IsUnique()
            .HasFilter("exchange_funding_id IS NOT NULL");
    }
}

public sealed class TransferConfiguration : IEntityTypeConfiguration<Transfer>
{
    public void Configure(EntityTypeBuilder<Transfer> b)
    {
        b.ToTable("transfers");
        b.HasKey(t => t.Id);

        b.Property(t => t.Asset).HasMaxLength(32).IsRequired();
        b.Property(t => t.Network).HasMaxLength(64);
        b.Property(t => t.TxHash).HasMaxLength(128);
        b.Property(t => t.Counterparty).HasMaxLength(128);
        b.Property(t => t.Note).HasMaxLength(1000);

        b.Property(t => t.Amount).HasColumnType(Precision.Quantity);
        b.Property(t => t.Fee).HasColumnType(Precision.Quantity);
        b.Property(t => t.ValueUsd).HasColumnType(Precision.Money);

        b.HasOne(t => t.FromAccount).WithMany()
            .HasForeignKey(t => t.FromAccountId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(t => t.ToAccount).WithMany()
            .HasForeignKey(t => t.ToAccountId).OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(t => new { t.UserId, t.OccurredAt }).IsDescending(false, true);
    }
}

public sealed class HoldingConfiguration : IEntityTypeConfiguration<Holding>
{
    public void Configure(EntityTypeBuilder<Holding> b)
    {
        b.ToTable("holdings");
        b.HasKey(h => h.Id);

        b.Property(h => h.Asset).HasMaxLength(64).IsRequired();
        b.Property(h => h.PoolName).HasMaxLength(128);
        b.Property(h => h.Note).HasMaxLength(1000);

        b.Property(h => h.Quantity).HasColumnType(Precision.Quantity);
        b.Property(h => h.AverageEntryPrice).HasColumnType(Precision.Price);
        b.Property(h => h.CurrentPrice).HasColumnType(Precision.Price);
        b.Property(h => h.EntryValueUsd).HasColumnType(Precision.Money);
        b.Property(h => h.CurrentValueUsd).HasColumnType(Precision.Money);
        b.Property(h => h.FarmApr).HasColumnType(Precision.Ratio);

        b.HasOne(h => h.Account).WithMany(a => a.Holdings)
            .HasForeignKey(h => h.AccountId).OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(h => new { h.UserId, h.Kind, h.Asset });
    }
}

public sealed class BalanceSnapshotConfiguration : IEntityTypeConfiguration<BalanceSnapshot>
{
    public void Configure(EntityTypeBuilder<BalanceSnapshot> b)
    {
        b.ToTable("balance_snapshots");
        b.HasKey(s => s.Id);

        b.Property(s => s.Asset).HasMaxLength(16);
        b.Property(s => s.WalletBalance).HasColumnType(Precision.Money);
        b.Property(s => s.Available).HasColumnType(Precision.Money);
        b.Property(s => s.Frozen).HasColumnType(Precision.Money);
        b.Property(s => s.Margin).HasColumnType(Precision.Money);
        b.Property(s => s.UnrealizedPnl).HasColumnType(Precision.Money);
        b.Property(s => s.Equity).HasColumnType(Precision.Money);
        b.Property(s => s.Bonus).HasColumnType(Precision.Money);

        b.HasOne(s => s.Account).WithMany(a => a.BalanceSnapshots)
            .HasForeignKey(s => s.AccountId).OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(s => new { s.AccountId, s.CapturedAt }).IsDescending(false, true);
        b.HasIndex(s => new { s.UserId, s.CapturedAt }).IsDescending(false, true);
    }
}

public sealed class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> b)
    {
        b.ToTable("attachments");
        b.HasKey(a => a.Id);

        b.Property(a => a.Slot).HasMaxLength(32).IsRequired();
        b.Property(a => a.StorageKey).HasMaxLength(512).IsRequired();
        b.Property(a => a.FileName).HasMaxLength(256).IsRequired();
        b.Property(a => a.ContentType).HasMaxLength(128).IsRequired();
        b.Property(a => a.Caption).HasMaxLength(500);

        b.HasOne(a => a.Trade).WithMany(t => t.Attachments)
            .HasForeignKey(a => a.TradeId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(a => a.Transfer).WithMany(t => t.Attachments)
            .HasForeignKey(a => a.TransferId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class SyncCursorConfiguration : IEntityTypeConfiguration<SyncCursor>
{
    public void Configure(EntityTypeBuilder<SyncCursor> b)
    {
        b.ToTable("sync_cursors");
        b.HasKey(c => c.Id);
        b.Property(c => c.Endpoint).HasMaxLength(128).IsRequired();
        b.Property(c => c.LastRecordId).HasMaxLength(64);

        b.HasOne(c => c.Account).WithMany()
            .HasForeignKey(c => c.AccountId).OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(c => new { c.AccountId, c.Endpoint }).IsUnique();
    }
}

public sealed class SyncRunConfiguration : IEntityTypeConfiguration<SyncRun>
{
    public void Configure(EntityTypeBuilder<SyncRun> b)
    {
        b.ToTable("sync_runs");
        b.HasKey(r => r.Id);
        b.Property(r => r.Endpoint).HasMaxLength(128).IsRequired();
        b.Property(r => r.Error).HasMaxLength(4000);

        b.HasOne(r => r.Account).WithMany()
            .HasForeignKey(r => r.AccountId).OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(r => new { r.AccountId, r.StartedAt }).IsDescending(false, true);
    }
}

public sealed class RawExchangePayloadConfiguration : IEntityTypeConfiguration<RawExchangePayload>
{
    public void Configure(EntityTypeBuilder<RawExchangePayload> b)
    {
        b.ToTable("raw_exchange_payloads");
        b.HasKey(p => p.Id);

        b.Property(p => p.Endpoint).HasMaxLength(128).IsRequired();
        b.Property(p => p.ExternalId).HasMaxLength(64).IsRequired();
        b.Property(p => p.Payload).HasColumnType("jsonb").IsRequired();

        b.HasIndex(p => new { p.AccountId, p.Endpoint, p.ExternalId }).IsUnique();
    }
}
