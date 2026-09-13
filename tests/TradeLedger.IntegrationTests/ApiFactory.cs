using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TradeLedger.Api.Shared;
using TradeLedger.Core.Domain;

namespace TradeLedger.IntegrationTests;

/// Hosts the real API against the test container. Program.cs reads Cors, the
/// connection strings and the JWT key while it registers services, which is before
/// any WebApplicationFactory callback can contribute, so those go through the
/// environment; the in-memory source repeats them for anything read later.
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly Dictionary<string, string> _settings;

    public ApiFactory(string connectionString, string allowedOrigin = "http://localhost:5173")
    {
        _settings = new Dictionary<string, string>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Testing",
            ["ConnectionStrings__Postgres"] = connectionString,

            // Blank means "no Redis", which swaps in the no-op lock and rate limiters.
            // A single space rather than "" because Windows deletes an empty variable.
            ["ConnectionStrings__Redis"] = " ",
            ["Cors__AllowedOrigins__0"] = allowedOrigin,
            ["Jwt__SigningKey"] = new string('k', 48),
            ["Encryption__KeyBase64"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),

            // No owner seed: these tests create the user they need.
            ["Seed__OwnerEmail"] = " ",
        };

        foreach (var (key, value) in _settings)
        {
            Environment.SetEnvironmentVariable(key, value);
        }
    }

    /// A fresh user per call, with the bearer token that identifies it. Every
    /// user-owned table is behind a global query filter, so a test that wants to see
    /// only its own rows just asks for its own user.
    public async Task<(Guid UserId, string Token)> CreateUserAsync()
    {
        using var scope = Services.CreateScope();

        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var tokens = scope.ServiceProvider.GetRequiredService<JwtTokenService>();

        var email = $"test-{Guid.CreateVersion7():N}@tradeledger.test";

        var user = new AppUser
        {
            Id = Guid.CreateVersion7(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
        };

        var created = await users.CreateAsync(user, "a-long-test-password");

        if (!created.Succeeded)
        {
            throw new InvalidOperationException(
                string.Join("; ", created.Errors.Select(e => e.Description)));
        }

        return (user.Id, tokens.Issue(user).Token);
    }

    public HttpClient CreateClientFor(string token)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
            _settings
                .Where(pair => !pair.Key.StartsWith("ASPNETCORE_", StringComparison.Ordinal))
                .ToDictionary(pair => pair.Key.Replace("__", ":"), pair => (string?)pair.Value)));
    }

    protected override void Dispose(bool disposing)
    {
        foreach (var key in _settings.Keys)
        {
            Environment.SetEnvironmentVariable(key, null);
        }

        base.Dispose(disposing);
    }
}
