using Microsoft.AspNetCore.Identity;

namespace TradeLedger.Core.Domain;

public sealed class AppUser : IdentityUser<Guid>
{
    public string? DisplayName { get; set; }

    public decimal StartingBalance { get; set; }

    public DateTimeOffset? JournalStartedAt { get; set; }

    public decimal? DefaultRiskPerTrade { get; set; }

    public string TimeZoneId { get; set; } = "UTC";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<Account> Accounts { get; set; } = [];
}

public sealed class AppRole : IdentityRole<Guid>;

public interface IUserOwned
{
    Guid UserId { get; set; }
}
