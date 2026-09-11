using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradeLedger.Core.Domain;
using TradeLedger.Core.Shared;

namespace TradeLedger.Core.Persistence.Configurations.Portfolio;

public sealed class TransferConfiguration : IEntityTypeConfiguration<Transfer>
{
    public void Configure(EntityTypeBuilder<Transfer> b)
    {
        b.ToTable("transfers", schema: DatabaseConsts.CoreSchema);
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

