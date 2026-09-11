using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradeLedger.Core.Domain.Backtesting;
using TradeLedger.Core.Shared;

namespace TradeLedger.Core.Persistence.Configurations.Backtesting;

public sealed class BacktestAccountConfiguration : IEntityTypeConfiguration<BacktestAccount>
{
    public void Configure(EntityTypeBuilder<BacktestAccount> b)
    {
        b.ToTable("backtest_accounts", schema: DatabaseConsts.BacktestSchema);
        b.HasKey(a => a.Id);

        b.Property(a => a.Name).HasMaxLength(128).IsRequired();
        b.Property(a => a.Description).HasMaxLength(1000);
        b.Property(a => a.Currency).HasMaxLength(16).IsRequired();
        b.Property(a => a.StartingBalance).HasColumnType(Precision.Money);

        b.HasOne(a => a.BacktestStrategy)
            .WithMany()
            .HasForeignKey(a => a.BacktestStrategyId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(a => new { a.UserId, a.Name }).IsUnique();
        b.HasIndex(a => new { a.UserId, a.CreatedAt });
    }
}

