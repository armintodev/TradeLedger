using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TradeLedger.Core.Domain;
using TradeLedger.Core.Shared;
using TradeLedger.Core.Shared.Proxy;

namespace TradeLedger.IntegrationTests;

[Collection(nameof(PostgresCollection))]
public sealed class UserProxyResolverTests(PostgresFixture fixture)
{
    private static readonly ICredentialProtector Protector = new AesGcmCredentialProtector(
        Options.Create(new EncryptionOptions
        {
            KeyBase64 = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
        }));

    [Fact]
    public async Task ResolvesTheProxyStoredOnTheUser()
    {
        var userId = await AddUserWithProxyAsync(
            ProxyScheme.Socks5, "10.20.30.40", 1080, "tunnel", "secret", enabled: true);

        var proxy = await ResolveAsync(userId);

        Assert.NotNull(proxy);
        Assert.Equal(ProxyScheme.Socks5, proxy.Scheme);
        Assert.Equal("10.20.30.40", proxy.Host);
        Assert.Equal(1080, proxy.Port);
        Assert.Equal("tunnel", proxy.Username);
        Assert.Equal("socks5://10.20.30.40:1080/", proxy.ToUri().ToString());
    }

    [Fact]
    public async Task DecryptsTheProxyPasswordForUse()
    {
        var userId = await AddUserWithProxyAsync(
            ProxyScheme.Socks5, "10.20.30.40", 1080, "tunnel", "p@ssw0rd!", enabled: true);

        var proxy = await ResolveAsync(userId);

        Assert.Equal("p@ssw0rd!", proxy!.Password);
        Assert.True(proxy.UseCredentials);
    }

    [Fact]
    public async Task StoresTheProxyPasswordEncryptedNotInPlaintext()
    {
        var userId = await AddUserWithProxyAsync(
            ProxyScheme.Socks5, "10.20.30.40", 1080, "tunnel", "p@ssw0rd!", enabled: true);

        await using var db = fixture.CreateContext(userId);
        var cipher = await db.Users
            .Where(u => u.Id == userId)
            .Select(u => u.ProxyPasswordCipher)
            .FirstAsync();

        Assert.NotNull(cipher);
        Assert.DoesNotContain("p@ssw0rd!", System.Text.Encoding.UTF8.GetString(cipher), StringComparison.Ordinal);
    }

    [Fact]
    public async Task FallsBackToConfigurationWhenTheUserProxyIsDisabled()
    {
        var userId = await AddUserWithProxyAsync(
            ProxyScheme.Socks5, "10.20.30.40", 1080, "tunnel", "secret", enabled: false);

        var proxy = await ResolveAsync(userId, new ProxyOptions
        {
            Scheme = ProxyScheme.Http,
            Host = "fallback.internal",
            Port = 3128,
        });

        Assert.NotNull(proxy);
        Assert.Equal("fallback.internal", proxy.Host);
        Assert.Equal(ProxyScheme.Http, proxy.Scheme);
    }

    [Fact]
    public async Task PrefersTheUserProxyOverConfiguration()
    {
        var userId = await AddUserWithProxyAsync(
            ProxyScheme.Socks5, "10.20.30.40", 1080, "tunnel", "secret", enabled: true);

        var proxy = await ResolveAsync(userId, new ProxyOptions
        {
            Scheme = ProxyScheme.Http,
            Host = "fallback.internal",
            Port = 3128,
        });

        Assert.Equal("10.20.30.40", proxy!.Host);
    }

    [Fact]
    public async Task ReturnsNullWhenNothingIsConfiguredAnywhere()
    {
        var userId = await AddUserWithProxyAsync(null, null, null, null, null, enabled: false);

        Assert.Null(await ResolveAsync(userId));
    }

    [Fact]
    public async Task ReturnsNullForAnUnknownUser()
    {
        Assert.Null(await ResolveAsync(Guid.CreateVersion7()));
    }

    [Fact]
    public async Task IgnoresAnEnabledButIncompleteUserProxy()
    {
        var userId = await AddUserWithProxyAsync(
            ProxyScheme.Socks5, host: null, port: 1080, null, null, enabled: true);

        Assert.Null(await ResolveAsync(userId));
    }

    private async Task<ProxyEndpoint?> ResolveAsync(Guid userId, ProxyOptions? options = null)
    {
        await using var db = fixture.CreateContext(userId);

        var resolver = new UserProxyResolver(
            db, Protector, Options.Create(options ?? new ProxyOptions()));

        return await resolver.ResolveAsync(userId);
    }

    private async Task<Guid> AddUserWithProxyAsync(
        ProxyScheme? scheme,
        string? host,
        int? port,
        string? username,
        string? password,
        bool enabled)
    {
        var userId = Guid.CreateVersion7();
        var email = $"{userId:N}@example.test";

        await using var db = fixture.CreateContext(userId);

        var user = new AppUser
        {
            Id = userId,
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            SecurityStamp = Guid.NewGuid().ToString(),
            PasswordHash = new PasswordHasher<AppUser>().HashPassword(null!, "not-used-in-tests"),
        };

        if (host is not null && scheme is not null && port is not null)
        {
            user.ConfigureProxy(scheme.Value, host, port.Value, username, enabled);
        }

        if (password is not null)
        {
            user.SetProxyPassword(Protector.Protect(password));
        }

        db.Users.Add(user);

        await db.SaveChangesAsync();
        return userId;
    }
}
