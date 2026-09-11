using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradeLedger.Core.Domain.Backtesting;
using TradeLedger.Core.Shared;

namespace TradeLedger.Core.Persistence.Configurations.Backtesting;

public sealed class BacktestRunConfiguration : IEntityTypeConfiguration<BacktestRun>
{
    public void Configure(EntityTypeBuilder<BacktestRun> b)
    {
        b.ToTable("backtest_runs", schema: DatabaseConsts.BacktestSchema);
        b.HasKey(r => r.Id);

        b.Property(r => r.Symbol).HasMaxLength(32);
        b.Property(r => r.RuleJson).HasColumnType("jsonb");
        b.Property(r => r.RuleHash).HasMaxLength(64);
        b.Property(r => r.ParametersJson).HasColumnType("jsonb").IsRequired();
        b.Property(r => r.WhatIfJson).HasColumnType("jsonb");
        b.Property(r => r.ResultJson).HasColumnType("jsonb");
        b.Property(r => r.WarningsJson).HasColumnType("jsonb");
        b.Property(r => r.Error).HasMaxLength(4000);

        b.Property(r => r.OpeningBalance).HasColumnType(Precision.Money);
        b.Property(r => r.ClosingBalance).HasColumnType(Precision.Money);

        b.Property(r => r.RiskPercentPerPosition).HasColumnType(Precision.Ratio);
        b.Property(r => r.RiskRewardRatio).HasColumnType(Precision.Ratio);
        b.Property(r => r.TakerFeeRate).HasColumnType(Precision.Ratio);
        b.Property(r => r.MakerFeeRate).HasColumnType(Precision.Ratio);
        b.Property(r => r.SlippageRate).HasColumnType(Precision.Ratio);
        b.Property(r => r.MaintenanceMarginRate).HasColumnType(Precision.Ratio);
        b.Property(r => r.ProgressPercent).HasColumnType(Precision.Ratio);

        b.HasOne(r => r.BacktestAccount)
            .WithMany(a => a.Runs)
            .HasForeignKey(r => r.BacktestAccountId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(r => r.BacktestStrategy)
            .WithMany()
            .HasForeignKey(r => r.BacktestStrategyId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(r => new { r.UserId, r.QueuedAt });
        b.HasIndex(r => new { r.Status, r.QueuedAt });
        b.HasIndex(r => new { r.BacktestAccountId, r.From });
    }
}

