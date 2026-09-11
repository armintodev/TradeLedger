using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradeLedger.Core.Domain;
using TradeLedger.Core.Shared;

namespace TradeLedger.Core.Persistence.Configurations.Accounts;

public sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> b)
    {
        b.ToTable("accounts", schema: DatabaseConsts.CoreSchema);
        b.HasKey(a => a.Id);
        b.Property(a => a.Name).HasMaxLength(128).IsRequired();
        b.Property(a => a.QuoteAsset).HasMaxLength(16);

        b.HasOne(a => a.User).WithMany(u => u.Accounts)
            .HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(a => new { a.UserId, a.Name }).IsUnique();
    }
}

