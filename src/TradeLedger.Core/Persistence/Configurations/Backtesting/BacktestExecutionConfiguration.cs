using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradeLedger.Core.Domain.Backtesting;
using TradeLedger.Core.Shared;

namespace TradeLedger.Core.Persistence.Configurations.Backtesting;

public sealed class BacktestExecutionConfiguration : IEntityTypeConfiguration<BacktestExecution>
{
    public void Configure(EntityTypeBuilder<BacktestExecution> b)
    {
        b.ToTable("backtest_executions", schema: DatabaseConsts.BacktestSchema);
        b.HasKey(e => e.Id);

        b.Property(e => e.Price).HasColumnType(Precision.Price);
        b.Property(e => e.Quantity).HasColumnType(Precision.Quantity);
        b.Property(e => e.Fee).HasColumnType(Precision.Money);

        b.HasOne(e => e.BacktestTrade)
            .WithMany(t => t.Executions)
            .HasForeignKey(e => e.BacktestTradeId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(e => new { e.BacktestTradeId, e.BarIndex });
    }
}

