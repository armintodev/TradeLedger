using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradeLedger.Core.Domain;
using TradeLedger.Core.Shared;

namespace TradeLedger.Core.Persistence.Configurations.Accounts;

public sealed class ExchangeCredentialConfiguration : IEntityTypeConfiguration<ExchangeCredential>
{
    public void Configure(EntityTypeBuilder<ExchangeCredential> b)
    {
        b.ToTable("exchange_credentials", schema: DatabaseConsts.CoreSchema);
        b.HasKey(c => c.Id);

        b.Property(c => c.ApiKeyHint).HasMaxLength(16).IsRequired();
        b.Property(c => c.Label).HasMaxLength(128);
        b.Property(c => c.LastVerificationError).HasMaxLength(1000);
        b.Property(c => c.VerifiedViaEgress).HasMaxLength(256);

        b.HasOne(c => c.Account).WithOne(a => a.Credential)
            .HasForeignKey<ExchangeCredential>(c => c.AccountId).OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(c => c.AccountId).IsUnique();
    }
}

