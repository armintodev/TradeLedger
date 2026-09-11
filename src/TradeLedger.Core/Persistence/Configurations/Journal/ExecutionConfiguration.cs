using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradeLedger.Core.Domain;
using TradeLedger.Core.Shared;

namespace TradeLedger.Core.Persistence.Configurations.Journal;

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

