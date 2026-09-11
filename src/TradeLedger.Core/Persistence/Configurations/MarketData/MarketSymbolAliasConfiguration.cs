using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradeLedger.Core.Domain.MarketData;
using TradeLedger.Core.MarketData;
using TradeLedger.Core.Shared;

namespace TradeLedger.Core.Persistence.Configurations.MarketData;

public sealed class MarketSymbolAliasConfiguration : IEntityTypeConfiguration<MarketSymbolAlias>
{
    public void Configure(EntityTypeBuilder<MarketSymbolAlias> b)
    {
        b.ToTable("market_symbol_aliases", schema: DatabaseConsts.MarketSchema);
        b.HasKey(a => a.Id);
        b.Property(a => a.Id).ValueGeneratedOnAdd();

        b.Property(a => a.CanonicalSymbol).HasMaxLength(32).IsRequired();
        b.Property(a => a.SourceSymbol).HasMaxLength(32).IsRequired();

        b.HasIndex(a => new { a.CanonicalSymbol, a.Source }).IsUnique();
    }
}

