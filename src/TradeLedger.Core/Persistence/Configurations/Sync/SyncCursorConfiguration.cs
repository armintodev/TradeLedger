using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradeLedger.Core.Domain;
using TradeLedger.Core.Shared;

namespace TradeLedger.Core.Persistence.Configurations.Sync;

public sealed class SyncCursorConfiguration : IEntityTypeConfiguration<SyncCursor>
{
    public void Configure(EntityTypeBuilder<SyncCursor> b)
    {
        b.ToTable("sync_cursors", schema: DatabaseConsts.CoreSchema);
        b.HasKey(c => c.Id);
        b.Property(c => c.Endpoint).HasMaxLength(128).IsRequired();
        b.Property(c => c.LastRecordId).HasMaxLength(64);

        b.HasOne(c => c.Account).WithMany()
            .HasForeignKey(c => c.AccountId).OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(c => new { c.AccountId, c.Endpoint }).IsUnique();
    }
}

