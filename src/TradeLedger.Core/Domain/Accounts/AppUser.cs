using Microsoft.AspNetCore.Identity;
using TradeLedger.Core.Shared.Proxy;

namespace TradeLedger.Core.Domain;

public sealed class AppUser : IdentityUser<Guid>
{
    private readonly List<Account> _accounts = [];

    public string? DisplayName { get; private set; }

    public decimal StartingBalance { get; private set; }

    public DateTimeOffset? JournalStartedAt { get; private set; }

    public decimal? DefaultRiskPerTrade { get; private set; }

    public string TimeZoneId { get; private set; } = "UTC";

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    public ProxyScheme? ProxyScheme { get; private set; }

    public string? ProxyHost { get; private set; }

    public int? ProxyPort { get; private set; }

    public string? ProxyUsername { get; private set; }

    public byte[]? ProxyPasswordCipher { get; private set; }

    public bool ProxyEnabled { get; private set; }

    public IReadOnlyCollection<Account> Accounts => _accounts;

    public bool HasProxy => ProxyEnabled && !string.IsNullOrWhiteSpace(ProxyHost);

    public bool HasProxyPassword => ProxyPasswordCipher is { Length: > 0 };

    public void SetDisplayName(string? displayName)
    {
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim();
    }

    public void StartJournal(decimal startingBalance, DateTimeOffset? startedAt)
    {
        StartingBalance = Guard.NotNegative(startingBalance, nameof(startingBalance));
        JournalStartedAt = startedAt;
    }

    public void SetDefaultRiskPerTrade(decimal? riskFraction)
    {
        if (riskFraction is { } risk)
        {
            Guard.InRange(risk, 0.0001m, 1m, nameof(riskFraction));
        }

        DefaultRiskPerTrade = riskFraction;
    }

    public void SetTimeZone(string timeZoneId)
    {
        TimeZoneId = Guard.NotBlank(timeZoneId, nameof(timeZoneId));
    }

    public ProxyEndpoint? CurrentProxy() =>
        ProxyEndpoint.TryCreate(ProxyScheme, ProxyHost, ProxyPort, ProxyUsername, null);

    public void ConfigureProxy(
        ProxyScheme scheme,
        string host,
        int port,
        string? username,
        bool enabled)
    {
        ProxyScheme = scheme;
        ProxyHost = Guard.NotBlank(host, nameof(host));
        ProxyPort = Guard.InRange(port, 1, 65535, nameof(port));
        ProxyUsername = string.IsNullOrWhiteSpace(username) ? null : username.Trim();
        ProxyEnabled = enabled;
    }

    public void SetProxyPassword(byte[] passwordCipher)
    {
        ProxyPasswordCipher = passwordCipher;
    }

    public void ClearProxyPassword()
    {
        ProxyPasswordCipher = null;
    }

    public void RemoveProxy()
    {
        ProxyScheme = null;
        ProxyHost = null;
        ProxyPort = null;
        ProxyUsername = null;
        ProxyPasswordCipher = null;
        ProxyEnabled = false;
    }
}

public sealed class AppRole : IdentityRole<Guid>;
