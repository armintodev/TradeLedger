using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradeLedger.Core.Domain;
using TradeLedger.Core.Shared;

namespace TradeLedger.Core.Persistence.Configurations.Portfolio;

public sealed class BalanceSnapshotConfiguration : IEntityTypeConfiguration<BalanceSnapshot>
{
    public void Configure(EntityTypeBuilder<BalanceSnapshot> b)
    {
        b.ToTable("balance_snapshots", schema: DatabaseConsts.CoreSchema);
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

