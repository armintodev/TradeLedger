using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradeLedger.Core.Domain;
using TradeLedger.Core.Shared;

namespace TradeLedger.Core.Persistence.Configurations.Portfolio;

public sealed class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> b)
    {
        b.ToTable("attachments", schema: DatabaseConsts.CoreSchema);
        b.HasKey(a => a.Id);

        b.Property(a => a.Slot).HasMaxLength(32).IsRequired();
        b.Property(a => a.StorageKey).HasMaxLength(512).IsRequired();
        b.Property(a => a.FileName).HasMaxLength(256).IsRequired();
        b.Property(a => a.ContentType).HasMaxLength(128).IsRequired();
        b.Property(a => a.Caption).HasMaxLength(500);

        b.HasOne(a => a.Trade).WithMany(t => t.Attachments)
            .HasForeignKey(a => a.TradeId).OnDelete(DeleteBehavior.Cascade);

        b.HasOne(a => a.Transfer).WithMany(t => t.Attachments)
            .HasForeignKey(a => a.TransferId).OnDelete(DeleteBehavior.Cascade);
    }
}

