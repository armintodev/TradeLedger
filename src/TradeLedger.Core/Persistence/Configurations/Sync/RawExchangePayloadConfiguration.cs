using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradeLedger.Core.Domain;
using TradeLedger.Core.Shared;

namespace TradeLedger.Core.Persistence.Configurations.Sync;

public sealed class RawExchangePayloadConfiguration : IEntityTypeConfiguration<RawExchangePayload>
{
    public void Configure(EntityTypeBuilder<RawExchangePayload> b)
    {
        b.ToTable("raw_exchange_payloads", schema: DatabaseConsts.CoreSchema);
        b.HasKey(p => p.Id);

        b.Property(p => p.Endpoint).HasMaxLength(128).IsRequired();
        b.Property(p => p.ExternalId).HasMaxLength(64).IsRequired();
        b.Property(p => p.Payload).HasColumnType("jsonb").IsRequired();

        b.HasIndex(p => new { p.AccountId, p.Endpoint, p.ExternalId }).IsUnique();
    }
}
