using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradeLedger.Core.Domain.Backtesting;
using TradeLedger.Core.Shared;

namespace TradeLedger.Core.Persistence.Configurations.Backtesting;

public sealed class BacktestStrategyConfiguration : IEntityTypeConfiguration<BacktestStrategy>
{
    public void Configure(EntityTypeBuilder<BacktestStrategy> b)
    {
        b.ToTable("backtest_strategies", schema: DatabaseConsts.BacktestSchema);
        b.HasKey(s => s.Id);

        b.Property(s => s.Name).HasMaxLength(128).IsRequired();
        b.Property(s => s.Description).HasMaxLength(1000);
        b.Property(s => s.RuleJson).HasColumnType("jsonb").IsRequired();
        b.Property(s => s.RuleHash).HasMaxLength(64).IsRequired();

        b.HasOne(s => s.StrategyTerm)
            .WithMany()
            .HasForeignKey(s => s.StrategyTermId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(s => new { s.UserId, s.Name }).IsUnique();
    }
}

