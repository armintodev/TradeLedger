using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradeLedger.Core.Domain.MarketData;
using TradeLedger.Core.MarketData;
using TradeLedger.Core.Shared;

namespace TradeLedger.Core.Persistence.Configurations.MarketData;

public sealed class CandleConfiguration : IEntityTypeConfiguration<Candle>
{
    public void Configure(EntityTypeBuilder<Candle> b)
    {
        b.ToTable("candles", schema: DatabaseConsts.MarketSchema);
        b.HasKey(c => c.Id);
        b.Property(c => c.Id).ValueGeneratedOnAdd();

        b.Ignore(c => c.CloseTime);

        b.Property(c => c.Symbol).HasMaxLength(32).IsRequired();

        b.Property(c => c.Open).HasColumnType(Precision.Price);
        b.Property(c => c.High).HasColumnType(Precision.Price);
        b.Property(c => c.Low).HasColumnType(Precision.Price);
        b.Property(c => c.Close).HasColumnType(Precision.Price);
        b.Property(c => c.Volume).HasColumnType(Precision.Quantity);
        b.Property(c => c.QuoteVolume).HasColumnType(Precision.Quantity);

        b.HasIndex(c => new { c.Source, c.Symbol, c.Interval, c.OpenTime }).IsUnique();
    }
}

