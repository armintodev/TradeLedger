using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TradeLedger.Core.Domain;
using TradeLedger.Core.Persistence;
using TradeLedger.Core.Shared;
using TradeLedger.Core.Shared.Proxy;

namespace TradeLedger.Api.Features.Proxy;

public static class ProxyEndpoints
{
    public static void MapProxyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/proxy").WithTags("Proxy").RequireAuthorization();

        group.MapGet("/", async (
            TradeLedgerDbContext db,
            IUserContext userContext,
            IOptions<ProxyOptions> proxyOptions,
            CancellationToken ct) =>
        {
            var userId = RequireUserId(userContext);

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
                    HasPassword = u.ProxyPasswordCipher != null,
                })
                .FirstOrDefaultAsync(ct);

            var options = proxyOptions.Value;
            var fallback = options.ToEndpoint();

            return Results.Ok(new ProxyResponse(
                Configured: user is { ProxyEnabled: true, ProxyHost: not null },
                Enabled: user?.ProxyEnabled ?? false,
                Scheme: user?.ProxyScheme,
                Host: user?.ProxyHost,
                Port: user?.ProxyPort,
                Username: user?.ProxyUsername,
                HasPassword: user?.HasPassword ?? false,
                Required: options.Required,
                ConfigurationFallback: fallback?.Describe()));
        })
        .WithName("GetProxy")
        .WithSummary("Show the current egress proxy")
        .WithDescription("The proxy every exchange request for this user is routed through. The password is never returned, only whether one is stored. `configurationFallback` is the proxy from the Proxy section of configuration, used when no per-user proxy is enabled.")
        .Produces<ProxyResponse>();

        group.MapPut("/", async (
            [FromBody] SetProxyRequest request,
            TradeLedgerDbContext db,
            IUserContext userContext,
            ICredentialProtector protector,
            IProxiedHttpClientProvider clients,
            CancellationToken ct) =>
        {
            var userId = RequireUserId(userContext);

            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
                ?? throw new ResourceNotFoundException("User", userId);

            var previous = user.CurrentProxy();

            user.ConfigureProxy(
                request.Scheme,
                request.Host,
                request.Port,
                request.Username,
                request.Enabled ?? true);

            if (!string.IsNullOrWhiteSpace(request.Password))
            {
                user.SetProxyPassword(protector.Protect(request.Password));
            }
            else if (request.ClearPassword == true)
            {
                user.ClearProxyPassword();
            }

            await db.SaveChangesAsync(ct);

            if (previous is not null)
            {
                clients.Remove(previous);
            }

            return Results.Ok(new ProxyUpdatedResponse(
                user.CurrentProxy()?.Describe() ?? "not configured",
                user.ProxyEnabled));
        })
        .WithName("SetProxy")
        .WithSummary("Set the egress proxy for this user")
        .WithDescription("Every Bitunix request for this user is routed through this proxy, so the exchange always sees one static IP. Supported schemes: Http, Https, Socks5, Socks4, Socks4a. The password is encrypted at rest and never returned. Cached connections for the previous proxy are dropped immediately. Set the proxy before attaching API credentials, because credential verification calls Bitunix and must originate from the allowlisted address.")
        .Produces<ProxyUpdatedResponse>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/", async (
            TradeLedgerDbContext db,
            IUserContext userContext,
            IProxiedHttpClientProvider clients,
            CancellationToken ct) =>
        {
            var userId = RequireUserId(userContext);

            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
                ?? throw new ResourceNotFoundException("User", userId);

            var previous = user.CurrentProxy();

            user.RemoveProxy();

            await db.SaveChangesAsync(ct);

            if (previous is not null)
            {
                clients.Remove(previous);
            }

            return Results.NoContent();
        })
        .WithName("DeleteProxy")
        .WithSummary("Remove the egress proxy")
        .WithDescription("Clears the per-user proxy. If Proxy:Required is true and no configuration fallback exists, every subsequent exchange request is refused rather than sent from the host address.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static Guid RequireUserId(IUserContext userContext) =>
        userContext.UserId
        ?? throw new NotAuthenticatedException();
}
