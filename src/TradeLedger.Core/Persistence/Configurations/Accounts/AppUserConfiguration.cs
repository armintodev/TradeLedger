using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradeLedger.Core.Domain;
using TradeLedger.Core.Shared;

namespace TradeLedger.Core.Persistence.Configurations.Accounts;

public sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> b)
    {
        b.Property(u => u.DisplayName).HasMaxLength(128);
        b.Property(u => u.TimeZoneId).HasMaxLength(64);
        b.Property(u => u.StartingBalance).HasColumnType(Precision.Money);
        b.Property(u => u.DefaultRiskPerTrade).HasColumnType(Precision.Ratio);
    }
}

