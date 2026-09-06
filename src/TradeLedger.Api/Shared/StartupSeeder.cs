using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TradeLedger.Core.Domain;
using TradeLedger.Core.Persistence;
using TradeLedger.Core.Persistence.Seed;

namespace TradeLedger.Api.Shared;

public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    public string? OwnerEmail { get; set; }

    public string? OwnerPassword { get; set; }
    public string? OwnerDisplayName { get; set; }
    public decimal StartingBalance { get; set; }
    public DateTimeOffset? JournalStartedAt { get; set; }
}

public static class StartupSeeder
{
    public static async Task SeedAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;

        var options = provider.GetRequiredService<
            Microsoft.Extensions.Options.IOptions<SeedOptions>>().Value;

        var db = provider.GetRequiredService<TradeLedgerDbContext>();
        var users = provider.GetRequiredService<UserManager<AppUser>>();
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("Seed");

        await db.Database.MigrateAsync(ct);

        if (string.IsNullOrWhiteSpace(options.OwnerEmail))
        {
            logger.LogInformation("Seed:OwnerEmail not configured; skipping owner seed.");
            return;
        }

        var user = await users.FindByEmailAsync(options.OwnerEmail);

        if (user is null)
        {
            if (string.IsNullOrWhiteSpace(options.OwnerPassword))
            {
                logger.LogWarning(
                    "Seed:OwnerEmail is set but Seed:OwnerPassword is not. " +
                    "Set it via user-secrets to create the owner account.");
                return;
            }

            user = new AppUser
            {
                Id = Guid.CreateVersion7(),
                UserName = options.OwnerEmail,
                Email = options.OwnerEmail,
                EmailConfirmed = true,
                DisplayName = options.OwnerDisplayName,
                StartingBalance = options.StartingBalance,
                JournalStartedAt = options.JournalStartedAt,
            };

            var result = await users.CreateAsync(user, options.OwnerPassword);
            if (!result.Succeeded)
            {
                logger.LogError(
                    "Could not create owner account: {Errors}",
                    string.Join("; ", result.Errors.Select(e => e.Description)));
                return;
            }

            logger.LogInformation("Owner account created for {Email}.", options.OwnerEmail);
        }

        db.BypassUserFilter = true;
        try
        {
            await TaxonomySeeder.SeedAsync(db, user.Id, ct);
        }
        finally
        {
            db.BypassUserFilter = false;
        }
    }
}
