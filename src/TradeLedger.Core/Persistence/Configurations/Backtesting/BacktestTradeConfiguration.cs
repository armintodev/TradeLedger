using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradeLedger.Core.Domain.Backtesting;
using TradeLedger.Core.Shared;

namespace TradeLedger.Core.Persistence.Configurations.Backtesting;

public sealed class BacktestTradeConfiguration : IEntityTypeConfiguration<BacktestTrade>
{
    public void Configure(EntityTypeBuilder<BacktestTrade> b)
    {
        b.ToTable("backtest_trades", schema: DatabaseConsts.BacktestSchema);
        b.HasKey(t => t.Id);

        b.Property(t => t.Symbol).HasMaxLength(32).IsRequired();
        b.Property(t => t.Notes).HasMaxLength(1000);

        b.Property(t => t.EntryPrice).HasColumnType(Precision.Price);
        b.Property(t => t.ExitPrice).HasColumnType(Precision.Price);
        b.Property(t => t.StopLossPrice).HasColumnType(Precision.Price);
        b.Property(t => t.TakeProfitPrice).HasColumnType(Precision.Price);
        b.Property(t => t.LiquidationPrice).HasColumnType(Precision.Price);

        b.Property(t => t.Quantity).HasColumnType(Precision.Quantity);

        b.Property(t => t.PositionMargin).HasColumnType(Precision.Money);
        b.Property(t => t.OrderValue).HasColumnType(Precision.Money);
        b.Property(t => t.GrossProfitLoss).HasColumnType(Precision.Money);
        b.Property(t => t.Fees).HasColumnType(Precision.Money);
        b.Property(t => t.Funding).HasColumnType(Precision.Money);
        b.Property(t => t.NetProfitLoss).HasColumnType(Precision.Money);
        b.Property(t => t.BalanceAfter).HasColumnType(Precision.Money);

        b.Property(t => t.AchievedReturnR).HasColumnType(Precision.Ratio);
        b.Property(t => t.PlannedReturnR).HasColumnType(Precision.Ratio);
        b.Property(t => t.TradeGainPercent).HasColumnType(Precision.Ratio);
        b.Property(t => t.MaeR).HasColumnType(Precision.Ratio);
        b.Property(t => t.MfeR).HasColumnType(Precision.Ratio);

        b.HasOne(t => t.BacktestRun)
            .WithMany(r => r.Trades)
            .HasForeignKey(t => t.BacktestRunId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(t => new { t.BacktestRunId, t.Sequence }).IsUnique();
    }
}

