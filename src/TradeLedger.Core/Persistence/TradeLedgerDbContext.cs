using System.Linq.Expressions;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TradeLedger.Core.Domain;
using TradeLedger.Core.Extensions;
using TradeLedger.Core.Shared;

namespace TradeLedger.Core.Persistence;

public sealed class TradeLedgerDbContext(
    DbContextOptions<TradeLedgerDbContext> options,
    IUserContext userContext
)
    : IdentityDbContext<AppUser, AppRole, Guid>(options)
{
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<ExchangeCredential> ExchangeCredentials => Set<ExchangeCredential>();
    public DbSet<Trade> Trades => Set<Trade>();
    public DbSet<Execution> Executions => Set<Execution>();
    public DbSet<TradePlan> TradePlans => Set<TradePlan>();
    public DbSet<TaxonomyTerm> TaxonomyTerms => Set<TaxonomyTerm>();
    public DbSet<TradeMistake> TradeMistakes => Set<TradeMistake>();
    public DbSet<TradeTracking> TradeTrackings => Set<TradeTracking>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<FundingPayment> FundingPayments => Set<FundingPayment>();
    public DbSet<Transfer> Transfers => Set<Transfer>();
    public DbSet<Holding> Holdings => Set<Holding>();
    public DbSet<BalanceSnapshot> BalanceSnapshots => Set<BalanceSnapshot>();
    public DbSet<SyncCursor> SyncCursors => Set<SyncCursor>();
    public DbSet<SyncRun> SyncRuns => Set<SyncRun>();
    public DbSet<RawExchangePayload> RawExchangePayloads => Set<RawExchangePayload>();

    public bool BypassUserFilter { get; set; }

    private Guid? CurrentUserId => userContext.UserId;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(TradeLedgerDbContext).Assembly);

        foreach (var entity in builder.Model.GetEntityTypes())
        {
            if (!typeof(IUserOwned).IsAssignableFrom(entity.ClrType))
            {
                continue;
            }

            var parameter = Expression.Parameter(entity.ClrType, "e");
            var userIdProperty = Expression.Property(parameter, nameof(IUserOwned.UserId));

            var currentUserId = Expression.Convert(
                Expression.Property(Expression.Constant(this), nameof(CurrentUserId)),
                typeof(Guid?)
            );

            var matchesTenant = Expression.Equal(
                Expression.Convert(userIdProperty, typeof(Guid?)),
                currentUserId
            );

            var bypass = Expression.Property(Expression.Constant(this), nameof(BypassUserFilter));

            var body = Expression.OrElse(bypass, matchesTenant);
            builder.Entity(entity.ClrType).HasQueryFilter(Expression.Lambda(body, parameter));
        }

        foreach (var entity in builder.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
            {
                var type = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;

                if (type.IsEnum)
                {
                    property.SetProviderClrType(typeof(string));
                }
            }
        }

        builder.RenameIdentityTableName();
    }

    public override int SaveChanges()
    {
        StampUserIds();

        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampUserIds();

        return base.SaveChangesAsync(cancellationToken);
    }

    private void StampUserIds()
    {
        if (CurrentUserId is not { } userId)
        {
            return;
        }

        foreach (var entry in ChangeTracker.Entries<IUserOwned>())
        {
            if (entry.State == EntityState.Added && entry.Entity.UserId == Guid.Empty)
            {
                entry.Entity.UserId = userId;
            }
        }
    }
}
