using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradeLedger.Core.Domain;
using TradeLedger.Core.Shared;

namespace TradeLedger.Core.Persistence.Configurations.Journal;

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

