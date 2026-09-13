using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TradeLedger.Api.Shared;
using TradeLedger.Api.Shared.Errors;
using TradeLedger.Core.Domain;
using TradeLedger.Core.Persistence;
using TradeLedger.Core.Shared;

namespace TradeLedger.Api.Features.Auth;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/login", async (
            [FromBody] LoginRequest request,
            UserManager<AppUser> users,
            JwtTokenService tokens,
            HttpContext http,
            CancellationToken ct) =>
        {
            var user = await users.FindByEmailAsync(request.Email);

            if (user is null || !await users.CheckPasswordAsync(user, request.Password))
            {
                // Shaped through ApiProblem like every other failure, so the response
                // carries a code and a traceId. The code deliberately does not say
                // which half was wrong.
                return Results.Problem(ApiProblem.From(
                    http,
                    StatusCodes.Status401Unauthorized,
                    "Invalid credentials",
                    "invalid_credentials",
                    "That email and password combination was not recognised."));
            }

            var (token, expiresAt) = tokens.Issue(user);
            return Results.Ok(new LoginResponse(token, expiresAt, user.Email!, user.DisplayName));
        })
        .WithName("Login")
        .WithSummary("Sign in and get a JWT")
        .WithDescription("Returns a bearer token for every other endpoint. Unknown email and wrong password give the same 401 on purpose, so the endpoint does not confirm which addresses exist. Public signup is deliberately absent: the owner account is seeded at startup from configuration.")
        .Produces<LoginResponse>()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .AllowAnonymous();

        group.MapGet("/me", async (
            UserManager<AppUser> users,
            HttpContext http,
            CancellationToken ct) =>
        {
            var user = await users.GetUserAsync(http.User);
            return user is null
                ? Results.Unauthorized()
                : Results.Ok(new MeResponse(
                    user.Id,
                    user.Email!,
                    user.DisplayName,
                    user.StartingBalance,
                    user.JournalStartedAt,
                    user.DefaultRiskPerTrade,
                    user.TimeZoneId));
        })
        .WithName("GetCurrentUser")
        .WithSummary("Current user profile")
        .WithDescription("The signed-in user plus their journal baseline: starting balance, journal start date, default risk per trade and display time zone. Everything is stored UTC; the time zone is for display only.")
        .Produces<MeResponse>()
        .RequireAuthorization();

        group.MapPut("/me/timezone", async (
            [FromBody] SetTimeZoneRequest request,
            TradeLedgerDbContext db,
            IUserContext userContext,
            CancellationToken ct) =>
        {
            var userId = userContext.UserId ?? throw new NotAuthenticatedException();

            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
                ?? throw new ResourceNotFoundException("User", userId);

            user.SetTimeZone(request.TimeZoneId);

            await db.SaveChangesAsync(ct);

            return Results.Ok(new TimeZoneResponse(user.TimeZoneId));
        })
        .WithName("SetCurrentUserTimeZone")
        .WithSummary("Set the display time zone")
        .WithDescription("Changes the zone every instant is rendered in. This is display only: instants stay UTC in the database and on the wire, so no journal, candle or backtest data moves. Takes an IANA id such as 'Asia/Tehran', or 'UTC'. Windows ids like 'Iran Standard Time' are refused, because the browser resolves this value through Intl.DateTimeFormat and cannot read them. The id is validated by shape, not against the host's time zone database, so the same request is accepted on every platform. Re-read GET /api/auth/me, or sign in again, for the new value to take effect.")
        .Produces<TimeZoneResponse>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .RequireAuthorization();
    }
}
