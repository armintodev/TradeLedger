using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradeLedger.Core.Domain.MarketData;
using TradeLedger.Core.MarketData;
using TradeLedger.Core.Shared;

namespace TradeLedger.Core.Persistence.Configurations.MarketData;

public sealed class MarketDataBackfillJobConfiguration : IEntityTypeConfiguration<MarketDataBackfillJob>
{
    public void Configure(EntityTypeBuilder<MarketDataBackfillJob> b)
    {
        b.ToTable("market_data_backfill_jobs", schema: DatabaseConsts.BacktestSchema);
        b.HasKey(j => j.Id);

        b.Property(j => j.Symbol).HasMaxLength(32).IsRequired();
        b.Property(j => j.Error).HasMaxLength(2000);
        b.Property(j => j.ProgressPercent).HasColumnType(Precision.Ratio);

        b.HasIndex(j => new { j.UserId, j.QueuedAt });
        b.HasIndex(j => new { j.Status, j.QueuedAt });
    }
}
