using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TradeLedger.Core.Persistence;

namespace TradeLedger.IntegrationTests;

/// <summary>
/// PUT /api/auth/me/timezone. The zone is display only, so the contract that matters
/// is that it round-trips through GET /api/auth/me and that a value the browser could
/// not resolve never reaches the column.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class TimeZoneEndpointTests(PostgresFixture fixture) : IAsyncLifetime
{
    private ApiFactory _factory = null!;

    public Task InitializeAsync()
    {
        _factory = new ApiFactory(fixture.ConnectionString);

        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task SettingTheZoneRoundTripsThroughMe()
    {
        var (userId, token) = await _factory.CreateUserAsync();
        var client = _factory.CreateClientFor(token);

        var before = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
        Assert.Equal("UTC", before.GetProperty("timeZoneId").GetString());

        var response = await client.PutAsJsonAsync(
            "/api/auth/me/timezone", new { timeZoneId = "Asia/Tehran" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Asia/Tehran", body.GetProperty("timeZoneId").GetString());

        var after = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
        Assert.Equal("Asia/Tehran", after.GetProperty("timeZoneId").GetString());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider
            .GetRequiredService<TradeLedgerDbContext>();

        var stored = await db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.TimeZoneId)
            .SingleAsync();

        Assert.Equal("Asia/Tehran", stored);
    }

    /// <summary>
    /// A Windows id resolves happily on a Windows host, which is exactly the trap:
    /// <c>Intl.DateTimeFormat</c> cannot read it, so the client would fall back to the
    /// browser zone and the screen would disagree with the journal in silence.
    /// </summary>
    [Theory]
    [InlineData("Iran Standard Time")]
    [InlineData("Nonsense")]
    [InlineData("Asia//Tehran")]
    [InlineData("")]
    public async Task AnIdTheBrowserCouldNotResolveIsRefused(string timeZoneId)
    {
        var (_, token) = await _factory.CreateUserAsync();
        var client = _factory.CreateClientFor(token);

        var response = await client.PutAsJsonAsync(
            "/api/auth/me/timezone", new { timeZoneId });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("validation_failed", problem.GetProperty("code").GetString());
        Assert.True(problem.TryGetProperty("errors", out var errors));
        Assert.True(errors.TryGetProperty("timeZoneId", out _));

        var after = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
        Assert.Equal("UTC", after.GetProperty("timeZoneId").GetString());
    }

    [Fact]
    public async Task TheEndpointRequiresAuthentication()
    {
        var client = _factory.CreateClient();

        var response = await client.PutAsJsonAsync(
            "/api/auth/me/timezone", new { timeZoneId = "Asia/Tehran" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// The reason the field exists at all: changing it must move no stored instant.
    /// </summary>
    [Fact]
    public async Task ChangingTheZoneMovesNoStoredInstant()
    {
        var (userId, token) = await _factory.CreateUserAsync();
        var client = _factory.CreateClientFor(token);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider
            .GetRequiredService<TradeLedgerDbContext>();

        var createdBefore = await db.Users
            .AsNoTracking().Where(u => u.Id == userId).Select(u => u.CreatedAt).SingleAsync();

        await client.PutAsJsonAsync("/api/auth/me/timezone", new { timeZoneId = "Asia/Tehran" });

        var createdAfter = await db.Users
            .AsNoTracking().Where(u => u.Id == userId).Select(u => u.CreatedAt).SingleAsync();

        Assert.Equal(
            createdBefore.ToUnixTimeMilliseconds(),
            createdAfter.ToUnixTimeMilliseconds());
        Assert.Equal(TimeSpan.Zero, createdAfter.Offset);
    }
}
