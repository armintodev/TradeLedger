using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradeLedger.Core.Domain.MarketData;
using TradeLedger.Core.MarketData;
using TradeLedger.Core.Shared;

namespace TradeLedger.Core.Persistence.Configurations.MarketData;

public sealed class FundingRateHistoryConfiguration : IEntityTypeConfiguration<FundingRateHistory>
{
    public void Configure(EntityTypeBuilder<FundingRateHistory> b)
    {
        b.ToTable("funding_rates", schema: DatabaseConsts.MarketSchema);
        b.HasKey(f => f.Id);
        b.Property(f => f.Id).ValueGeneratedOnAdd();

        b.Property(f => f.Symbol).HasMaxLength(32).IsRequired();
        b.Property(f => f.FundingRate).HasColumnType(Precision.Ratio);

        b.HasIndex(f => new { f.Source, f.Symbol, f.FundingTime }).IsUnique();
    }
}

