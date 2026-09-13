using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TradeLedger.Api.Shared;
using TradeLedger.Core.Domain;

namespace TradeLedger.IntegrationTests;

[Collection(nameof(PostgresCollection))]
public sealed class CorsPolicyTests(PostgresFixture fixture) : IAsyncLifetime
{
    private const string AllowedOrigin = "http://localhost:5173";
    private const string BlockedOrigin = "https://evil.example";
    private const string AllowOriginHeader = "Access-Control-Allow-Origin";

    private CorsApiFactory _factory = null!;

    public Task InitializeAsync()
    {
        _factory = new CorsApiFactory(fixture.ConnectionString, AllowedOrigin);

        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task PreflightFromAnAllowedOriginIsAnswered()
    {
        var response = await SendPreflightAsync(AllowedOrigin, "PATCH");

        Assert.True(response.IsSuccessStatusCode, $"Preflight returned {(int)response.StatusCode}.");
        Assert.Equal(AllowedOrigin, Single(response, AllowOriginHeader));
    }

    [Fact]
    public async Task PreflightFromAnUnknownOriginGetsNoAllowOriginHeader()
    {
        var response = await SendPreflightAsync(BlockedOrigin, "PATCH");

        Assert.False(response.Headers.Contains(AllowOriginHeader));
    }

    [Fact]
    public async Task AnAuthenticatedRequestCarriesTheAllowOriginHeader()
    {
        var token = await IssueTokenAsync();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/trades");
        request.Headers.Add("Origin", AllowedOrigin);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _factory.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(AllowedOrigin, Single(response, AllowOriginHeader));
    }

    // UseCors sits before UseAuthentication precisely so this holds: without it a 401
    // reads as a network failure in the browser instead of an expired session.
    [Fact]
    public async Task AnUnauthenticatedRequestStillCarriesTheAllowOriginHeader()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/trades");
        request.Headers.Add("Origin", AllowedOrigin);

        var response = await _factory.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(AllowedOrigin, Single(response, AllowOriginHeader));
    }

    private async Task<HttpResponseMessage> SendPreflightAsync(string origin, string method)
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/trades");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", method);

        return await _factory.CreateClient().SendAsync(request);
    }

    private async Task<string> IssueTokenAsync()
    {
        using var scope = _factory.Services.CreateScope();

        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var tokens = scope.ServiceProvider.GetRequiredService<JwtTokenService>();

        var email = $"cors-{Guid.CreateVersion7():N}@tradeledger.test";

        var user = new AppUser
        {
            Id = Guid.CreateVersion7(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
        };

        var created = await users.CreateAsync(user, "a-long-test-password");
        Assert.True(created.Succeeded, string.Join("; ", created.Errors.Select(e => e.Description)));

        return tokens.Issue(user).Token;
    }

    private static string? Single(HttpResponseMessage response, string header) =>
        response.Headers.TryGetValues(header, out var values) ? values.SingleOrDefault() : null;
}

/// Hosts the real API against the test container. Program.cs reads Cors, the
/// connection strings and the JWT key while it registers services, which is before
/// any WebApplicationFactory callback can contribute, so those go through the
/// environment; the in-memory source repeats them for anything read later.
public sealed class CorsApiFactory : WebApplicationFactory<Program>
{
    private readonly Dictionary<string, string> _settings;

    public CorsApiFactory(string connectionString, string allowedOrigin)
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
