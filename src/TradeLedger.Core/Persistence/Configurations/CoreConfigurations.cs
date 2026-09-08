using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradeLedger.Core.Domain;
using TradeLedger.Core.Shared;

namespace TradeLedger.Core.Persistence.Configurations;

public sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> b)
    {
        b.Property(u => u.DisplayName).HasMaxLength(128);
        b.Property(u => u.TimeZoneId).HasMaxLength(64);
        b.Property(u => u.StartingBalance).HasColumnType(Precision.Money);
        b.Property(u => u.DefaultRiskPerTrade).HasColumnType(Precision.Ratio);
    }
}

public sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> b)
    {
        b.ToTable("accounts", schema: DatabaseConsts.CoreSchema);
        b.HasKey(a => a.Id);
        b.Property(a => a.Name).HasMaxLength(128).IsRequired();
        b.Property(a => a.QuoteAsset).HasMaxLength(16);

        b.HasOne(a => a.User).WithMany(u => u.Accounts)
            .HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(a => new { a.UserId, a.Name }).IsUnique();
    }
}

public sealed class ExchangeCredentialConfiguration : IEntityTypeConfiguration<ExchangeCredential>
{
    public void Configure(EntityTypeBuilder<ExchangeCredential> b)
    {
        b.ToTable("exchange_credentials", schema: DatabaseConsts.CoreSchema);
        b.HasKey(c => c.Id);

        b.Property(c => c.ApiKeyHint).HasMaxLength(16).IsRequired();
        b.Property(c => c.Label).HasMaxLength(128);
        b.Property(c => c.LastVerificationError).HasMaxLength(1000);

        b.HasOne(c => c.Account).WithOne(a => a.Credential)
            .HasForeignKey<ExchangeCredential>(c => c.AccountId).OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(c => c.AccountId).IsUnique();
    }
}

public sealed class ExecutionConfiguration : IEntityTypeConfiguration<Execution>
{
    public void Configure(EntityTypeBuilder<Execution> b)
    {
        b.ToTable("executions", schema: DatabaseConsts.CoreSchema);
        b.HasKey(e => e.Id);

        b.Property(e => e.Price).HasColumnType(Precision.Price);
        b.Property(e => e.Quantity).HasColumnType(Precision.Quantity);
        b.Property(e => e.Fee).HasColumnType(Precision.Money);
        b.Property(e => e.RealizedProfitLoss).HasColumnType(Precision.Money);
        b.Property(e => e.FeeAsset).HasMaxLength(16);
        b.Property(e => e.ExchangeTradeId).HasMaxLength(64);
        b.Property(e => e.ExchangeOrderId).HasMaxLength(64);

        b.HasOne(e => e.Trade).WithMany(t => t.Executions)
            .HasForeignKey(e => e.TradeId).OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(e => new { e.AccountId, e.ExchangeTradeId })
            .IsUnique()
            .HasFilter("exchange_trade_id IS NOT NULL");

        b.HasIndex(e => new { e.TradeId, e.ExecutedAt });
    }
}

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

public sealed class TaxonomyTermConfiguration : IEntityTypeConfiguration<TaxonomyTerm>
{
    public void Configure(EntityTypeBuilder<TaxonomyTerm> b)
    {
        b.ToTable("taxonomy_terms", schema: DatabaseConsts.CoreSchema);
        b.HasKey(t => t.Id);
        b.Property(t => t.Name).HasMaxLength(128).IsRequired();
        b.Property(t => t.ColorHex).HasMaxLength(9);
        b.Property(t => t.Description).HasMaxLength(500);

        b.HasIndex(t => new { t.UserId, t.Kind, t.Name }).IsUnique();
    }
}

public sealed class TradeMistakeConfiguration : IEntityTypeConfiguration<TradeMistake>
{
    public void Configure(EntityTypeBuilder<TradeMistake> b)
    {
        b.ToTable("trade_mistakes", schema: DatabaseConsts.CoreSchema);
        b.HasKey(m => new { m.TradeId, m.TaxonomyTermId });
        b.Property(m => m.EstimatedCost).HasColumnType(Precision.Money);
        b.Property(m => m.Note).HasMaxLength(500);

        b.HasOne(m => m.Trade).WithMany(t => t.Mistakes)
            .HasForeignKey(m => m.TradeId).OnDelete(DeleteBehavior.Cascade);

        b.HasOne(m => m.Term).WithMany()
            .HasForeignKey(m => m.TaxonomyTermId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class TradeTrackingConfiguration : IEntityTypeConfiguration<TradeTracking>
{
    public void Configure(EntityTypeBuilder<TradeTracking> b)
    {
        b.ToTable("trade_trackings", schema: DatabaseConsts.CoreSchema);
        b.HasKey(t => new { t.TradeId, t.TaxonomyTermId });
        b.Property(t => t.Note).HasMaxLength(500);

        b.HasOne(t => t.Trade).WithMany(x => x.Trackings)
            .HasForeignKey(t => t.TradeId).OnDelete(DeleteBehavior.Cascade);

        b.HasOne(t => t.Term).WithMany()
            .HasForeignKey(t => t.TaxonomyTermId).OnDelete(DeleteBehavior.Cascade);
    }
}
