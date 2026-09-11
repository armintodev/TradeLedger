using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradeLedger.Core.Domain.Backtesting;
using TradeLedger.Core.Shared;

namespace TradeLedger.Core.Persistence.Configurations.Backtesting;

public sealed class BacktestEquityPointConfiguration : IEntityTypeConfiguration<BacktestEquityPoint>
{
    public void Configure(EntityTypeBuilder<BacktestEquityPoint> b)
    {
        b.ToTable("backtest_equity_points", schema: DatabaseConsts.BacktestSchema);
        b.HasKey(p => p.Id);

        b.Property(p => p.Equity).HasColumnType(Precision.Money);
        b.Property(p => p.Drawdown).HasColumnType(Precision.Money);
        b.Property(p => p.DrawdownPercent).HasColumnType(Precision.Ratio);

        b.HasOne(p => p.BacktestRun)
            .WithMany(r => r.EquityPoints)
            .HasForeignKey(p => p.BacktestRunId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(p => new { p.BacktestRunId, p.Sequence }).IsUnique();
    }
}
