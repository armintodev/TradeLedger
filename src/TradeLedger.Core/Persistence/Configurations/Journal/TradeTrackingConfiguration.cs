using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradeLedger.Core.Domain;
using TradeLedger.Core.Shared;

namespace TradeLedger.Core.Persistence.Configurations.Journal;

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
