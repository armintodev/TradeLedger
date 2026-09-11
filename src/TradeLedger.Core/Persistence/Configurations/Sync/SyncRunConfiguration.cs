using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradeLedger.Core.Domain;
using TradeLedger.Core.Shared;

namespace TradeLedger.Core.Persistence.Configurations.Sync;

public sealed class SyncRunConfiguration : IEntityTypeConfiguration<SyncRun>
{
    public void Configure(EntityTypeBuilder<SyncRun> b)
    {
        b.ToTable("sync_runs", schema: DatabaseConsts.CoreSchema);
        b.HasKey(r => r.Id);
        b.Property(r => r.Endpoint).HasMaxLength(128).IsRequired();
        b.Property(r => r.Error).HasMaxLength(4000);

        b.HasOne(r => r.Account).WithMany()
            .HasForeignKey(r => r.AccountId).OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(r => new { r.AccountId, r.StartedAt }).IsDescending(false, true);
    }
}

