using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradeLedger.Core.Domain.MarketData;
using TradeLedger.Core.MarketData;
using TradeLedger.Core.Shared;

namespace TradeLedger.Core.Persistence.Configurations.MarketData;

public sealed class CandleImportConfiguration : IEntityTypeConfiguration<CandleImport>
{
    public void Configure(EntityTypeBuilder<CandleImport> b)
    {
        b.ToTable("candle_imports", schema: DatabaseConsts.BacktestSchema);
        b.HasKey(i => i.Id);

        b.Property(i => i.FileName).HasMaxLength(260).IsRequired();
        b.Property(i => i.ContentSha256).HasMaxLength(64).IsRequired();
        b.Property(i => i.Symbol).HasMaxLength(32).IsRequired();
        b.Property(i => i.Warnings).HasMaxLength(4000);

        b.HasIndex(i => new { i.UserId, i.ImportedAt });
    }
}

