using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradeLedger.Core.Domain;
using TradeLedger.Core.Shared;

namespace TradeLedger.Core.Persistence.Configurations.Journal;

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

