using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using TradeLedger.Core.Shared;

namespace TradeLedger.Core.Persistence;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<TradeLedgerDbContext>
{
    public TradeLedgerDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("TRADELEDGER_DB")
            ?? "Host=localhost;Port=5432;Database=tradeledger;Username=tradeledger;Password=tradeledger";

        var options = new DbContextOptionsBuilder<TradeLedgerDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new TradeLedgerDbContext(options, new FixedUserContext(null));
    }
}
