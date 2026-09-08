using Microsoft.EntityFrameworkCore;
using TradeLedger.Core.Domain;
using TradeLedger.Core.Shared;

namespace TradeLedger.Core.Extensions;

public static class DbContextExtensions
{
    extension(ModelBuilder builder)
    {
        public void RenameIdentityTableName()
        {
            builder.Entity<AppUser>().ToTable("users", schema: DatabaseConsts.IdentitySchema);
            builder.Entity<AppRole>().ToTable("roles", schema: DatabaseConsts.IdentitySchema);
            // builder.Entity<IdentityUserRole<long>>().ToTable("user_roles");
            // builder.Entity<IdentityUserClaim<long>>().ToTable("user_claims");
            // builder.Entity<IdentityUserLogin<long>>().ToTable("user_logins");
            // builder.Entity<IdentityRoleClaim<long>>().ToTable("role_claims");
            // builder.Entity<IdentityUserToken<long>>().ToTable("user_tokens");
        }
    }
}
