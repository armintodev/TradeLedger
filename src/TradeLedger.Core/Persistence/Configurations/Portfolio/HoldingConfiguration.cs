using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradeLedger.Core.Domain;
using TradeLedger.Core.Shared;

namespace TradeLedger.Core.Persistence.Configurations.Portfolio;

public sealed class HoldingConfiguration : IEntityTypeConfiguration<Holding>
{
    public void Configure(EntityTypeBuilder<Holding> b)
    {
        b.ToTable("holdings", schema: DatabaseConsts.CoreSchema);
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

