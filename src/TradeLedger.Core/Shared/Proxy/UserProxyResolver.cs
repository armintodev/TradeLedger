using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TradeLedger.Core.Persistence;

namespace TradeLedger.Core.Shared.Proxy;

public sealed class ProxyOptions
{
    public const string SectionName = "Proxy";

    public bool Required { get; set; } = true;

    public ProxyScheme? Scheme { get; set; }

    public string? Host { get; set; }

    public int? Port { get; set; }

    public string? Username { get; set; }

    public string? Password { get; set; }

    public ProxyEndpoint? ToEndpoint() =>
        ProxyEndpoint.TryCreate(Scheme, Host, Port, Username, Password);
}

public interface IUserProxyResolver
{
    Task<ProxyEndpoint?> ResolveAsync(Guid userId, CancellationToken ct = default);
}

public sealed class UserProxyResolver(
    TradeLedgerDbContext db,
    ICredentialProtector protector,
    IOptions<ProxyOptions> options) : IUserProxyResolver
{
    private readonly ProxyOptions _options = options.Value;

    public async Task<ProxyEndpoint?> ResolveAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new
            {
                u.ProxyEnabled,
                u.ProxyScheme,
                u.ProxyHost,
                u.ProxyPort,
                u.ProxyUsername,
                u.ProxyPasswordCipher,
            })
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (user is { ProxyEnabled: true })
        {
            var password = user.ProxyPasswordCipher is { Length: > 0 }
                ? protector.Unprotect(user.ProxyPasswordCipher)
                : null;

            var fromUser = ProxyEndpoint.TryCreate(
                user.ProxyScheme, user.ProxyHost, user.ProxyPort, user.ProxyUsername, password);

            if (fromUser is not null)
            {
                return fromUser;
            }
        }

        return _options.ToEndpoint();
    }
}
