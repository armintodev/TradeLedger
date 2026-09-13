using System.Net;
using System.Net.Http.Headers;

namespace TradeLedger.IntegrationTests;

[Collection(nameof(PostgresCollection))]
public sealed class CorsPolicyTests(PostgresFixture fixture) : IAsyncLifetime
{
    private const string AllowedOrigin = "http://localhost:5173";
    private const string BlockedOrigin = "https://evil.example";
    private const string AllowOriginHeader = "Access-Control-Allow-Origin";

    private ApiFactory _factory = null!;

    public Task InitializeAsync()
    {
        _factory = new ApiFactory(fixture.ConnectionString, AllowedOrigin);

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

    private async Task<string> IssueTokenAsync() => (await _factory.CreateUserAsync()).Token;

    private static string? Single(HttpResponseMessage response, string header) =>
        response.Headers.TryGetValues(header, out var values) ? values.SingleOrDefault() : null;
}

