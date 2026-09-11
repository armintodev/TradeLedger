using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradeLedger.Core.Domain;
using TradeLedger.Core.Shared;

namespace TradeLedger.Core.Persistence.Configurations.Journal;

public sealed class TradePlanConfiguration : IEntityTypeConfiguration<TradePlan>
{
    public void Configure(EntityTypeBuilder<TradePlan> b)
    {
        b.ToTable("trade_plans", schema: DatabaseConsts.CoreSchema);
        b.HasKey(p => p.Id);

        b.Property(p => p.Symbol).HasMaxLength(32).IsRequired();
        b.Property(p => p.Notes).HasMaxLength(4000);

        b.Property(p => p.PlannedEntryPrice).HasColumnType(Precision.Price);
        b.Property(p => p.PlannedStopLossPrice).HasColumnType(Precision.Price);
        b.Property(p => p.PlannedTakeProfitPrice).HasColumnType(Precision.Price);
        b.Property(p => p.PlannedQuantity).HasColumnType(Precision.Quantity);
        b.Property(p => p.PlannedOrderValue).HasColumnType(Precision.Money);
        b.Property(p => p.PlannedMargin).HasColumnType(Precision.Money);
        b.Property(p => p.EstimatedProfit).HasColumnType(Precision.Money);
        b.Property(p => p.EstimatedLoss).HasColumnType(Precision.Money);
        b.Property(p => p.BalanceAtPlanning).HasColumnType(Precision.Money);
        b.Property(p => p.RiskFraction).HasColumnType(Precision.Ratio);
        b.Property(p => p.PlannedRiskReward).HasColumnType(Precision.Ratio);
        b.Property(p => p.AverageFeeRate).HasColumnType(Precision.Ratio);

        b.OwnsOne(
            p => p.MarketContext,
            mc =>
            {
                mc.Property(x => x.Total2).HasColumnName("ctx_total2").HasMaxLength(64);
                mc.Property(x => x.BtcDominance).HasColumnName("ctx_btc_dominance").HasMaxLength(64);
                mc.Property(x => x.UsdtDominance).HasColumnName("ctx_usdt_dominance").HasMaxLength(64);
                mc.Property(x => x.MarketTrend).HasColumnName("ctx_market_trend").HasMaxLength(64);
                mc.Property(x => x.Sma).HasColumnName("ctx_sma").HasMaxLength(64);
                mc.Property(x => x.MarketSession).HasColumnName("ctx_market_session").HasMaxLength(64);
                mc.Property(x => x.BtcPair).HasColumnName("ctx_btc_pair").HasMaxLength(64);
                mc.Property(x => x.Rsi).HasColumnName("ctx_rsi").HasMaxLength(64);
                mc.Property(x => x.Volume).HasColumnName("ctx_volume").HasMaxLength(64);
                mc.Property(x => x.CandleShape).HasColumnName("ctx_candle_shape").HasMaxLength(64);
            }
        );

        b.HasOne(p => p.Account).WithMany().HasForeignKey(p => p.AccountId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(p => p.Strategy).WithMany().HasForeignKey(p => p.StrategyId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(p => p.Timeframe).WithMany().HasForeignKey(p => p.TimeframeId).OnDelete(DeleteBehavior.SetNull);

        b.HasOne(p => p.EntryMentalState).WithMany()
            .HasForeignKey(p => p.EntryMentalStateId).OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(p => new { p.UserId, p.Status, p.Symbol });
    }
}

