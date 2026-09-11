using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradeLedger.Core.Domain;
using TradeLedger.Core.Shared;

namespace TradeLedger.Core.Persistence.Configurations.Portfolio;

public sealed class FundingPaymentConfiguration : IEntityTypeConfiguration<FundingPayment>
{
    public void Configure(EntityTypeBuilder<FundingPayment> b)
    {
        b.ToTable("funding_payments", schema: DatabaseConsts.CoreSchema);
        b.HasKey(f => f.Id);

        b.Property(f => f.Symbol).HasMaxLength(32).IsRequired();
        b.Property(f => f.Asset).HasMaxLength(16);
        b.Property(f => f.Amount).HasColumnType(Precision.Money);
        b.Property(f => f.FundingRate).HasColumnType(Precision.Ratio);
        b.Property(f => f.ExchangeFundingId).HasMaxLength(64);

        b.HasOne(f => f.Account).WithMany()
            .HasForeignKey(f => f.AccountId).OnDelete(DeleteBehavior.Cascade);

        b.HasOne(f => f.Trade).WithMany(t => t.FundingPayments)
            .HasForeignKey(f => f.TradeId).OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(f => new { f.AccountId, f.ExchangeFundingId })
            .IsUnique()
            .HasFilter("exchange_funding_id IS NOT NULL");
    }
}

