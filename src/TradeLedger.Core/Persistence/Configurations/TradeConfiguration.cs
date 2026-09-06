using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradeLedger.Core.Domain;
using TradeLedger.Core.Shared;

namespace TradeLedger.Core.Persistence.Configurations;

public sealed class TradeConfiguration : IEntityTypeConfiguration<Trade>
{
    public void Configure(EntityTypeBuilder<Trade> b)
    {
        b.ToTable("trades");
        b.HasKey(t => t.Id);

        b.Property(t => t.Symbol).HasMaxLength(32).IsRequired();
        b.Property(t => t.ExchangePositionId).HasMaxLength(64);
        b.Property(t => t.Tag).HasMaxLength(128);
        b.Property(t => t.PostTradeTag).HasMaxLength(128);
        b.Property(t => t.Memo).HasMaxLength(4000);

        b.Property(t => t.EntryPrice).HasColumnType(Precision.Price);
        b.Property(t => t.ExitPrice).HasColumnType(Precision.Price);
        b.Property(t => t.StopLossPrice).HasColumnType(Precision.Price);
        b.Property(t => t.TakeProfitPrice).HasColumnType(Precision.Price);
        b.Property(t => t.LiquidationPrice).HasColumnType(Precision.Price);

        b.Property(t => t.Quantity).HasColumnType(Precision.Quantity);
        b.Property(t => t.LiquidatedQuantity).HasColumnType(Precision.Quantity);

        b.Property(t => t.PositionMargin).HasColumnType(Precision.Money);
        b.Property(t => t.OrderValue).HasColumnType(Precision.Money);
        b.Property(t => t.GrossProfitLoss).HasColumnType(Precision.Money);
        b.Property(t => t.Fees).HasColumnType(Precision.Money);
        b.Property(t => t.Funding).HasColumnType(Precision.Money);
        b.Property(t => t.NetProfitLoss).HasColumnType(Precision.Money);
        b.Property(t => t.BalanceAfter).HasColumnType(Precision.Money);

        b.Property(t => t.PercentClosed).HasColumnType(Precision.Ratio);
        b.Property(t => t.PositionToAccountPercent).HasColumnType(Precision.Ratio);
        b.Property(t => t.PlannedStopLossPercent).HasColumnType(Precision.Ratio);
        b.Property(t => t.AccountRiskedPercent).HasColumnType(Precision.Ratio);
        b.Property(t => t.PlannedReturnR).HasColumnType(Precision.Ratio);
        b.Property(t => t.AchievedReturnR).HasColumnType(Precision.Ratio);
        b.Property(t => t.TradeGainPercent).HasColumnType(Precision.Ratio);
        b.Property(t => t.AccountChangePercent).HasColumnType(Precision.Ratio);

        b.OwnsOne(t => t.MarketContext, mc =>
        {
            mc.Property(p => p.Total2).HasColumnName("ctx_total2").HasMaxLength(64);
            mc.Property(p => p.BtcDominance).HasColumnName("ctx_btc_dominance").HasMaxLength(64);
            mc.Property(p => p.UsdtDominance).HasColumnName("ctx_usdt_dominance").HasMaxLength(64);
            mc.Property(p => p.MarketTrend).HasColumnName("ctx_market_trend").HasMaxLength(64);
            mc.Property(p => p.Sma).HasColumnName("ctx_sma").HasMaxLength(64);
            mc.Property(p => p.MarketSession).HasColumnName("ctx_market_session").HasMaxLength(64);
            mc.Property(p => p.BtcPair).HasColumnName("ctx_btc_pair").HasMaxLength(64);
            mc.Property(p => p.Rsi).HasColumnName("ctx_rsi").HasMaxLength(64);
            mc.Property(p => p.Volume).HasColumnName("ctx_volume").HasMaxLength(64);
            mc.Property(p => p.CandleShape).HasColumnName("ctx_candle_shape").HasMaxLength(64);
        });

        b.HasOne(t => t.Account).WithMany(a => a.Trades)
            .HasForeignKey(t => t.AccountId).OnDelete(DeleteBehavior.Cascade);

        b.HasOne(t => t.Strategy).WithMany().HasForeignKey(t => t.StrategyId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(t => t.Timeframe).WithMany().HasForeignKey(t => t.TimeframeId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(t => t.EntryType).WithMany().HasForeignKey(t => t.EntryTypeId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(t => t.ExitType).WithMany().HasForeignKey(t => t.ExitTypeId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(t => t.EntryMentalState).WithMany()
            .HasForeignKey(t => t.EntryMentalStateId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(t => t.ExitMentalState).WithMany()
            .HasForeignKey(t => t.ExitMentalStateId).OnDelete(DeleteBehavior.SetNull);

        b.HasOne(t => t.TradePlan).WithOne(p => p.LinkedTrade)
            .HasForeignKey<Trade>(t => t.TradePlanId).OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(t => new { t.AccountId, t.ExchangePositionId })
            .IsUnique()
            .HasFilter("exchange_position_id IS NOT NULL");

        b.HasIndex(t => new { t.UserId, t.OpenedAt }).IsDescending(false, true);

        b.HasIndex(t => new { t.UserId, t.ReviewState });

        b.HasIndex(t => new { t.UserId, t.StrategyId });
        b.HasIndex(t => new { t.UserId, t.Symbol });
    }
}
