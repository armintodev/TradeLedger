using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TradeLedger.Api.Shared;
using TradeLedger.Core.Domain;

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
            CancellationToken ct) =>
        {
            var user = await users.FindByEmailAsync(request.Email);

            if (user is null || !await users.CheckPasswordAsync(user, request.Password))
            {
                return Results.Problem(
                    title: "Invalid credentials",
                    statusCode: StatusCodes.Status401Unauthorized);
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
    }
}

public sealed record LoginRequest(string Email, string Password);

public sealed record LoginResponse(
    string Token,
    DateTimeOffset ExpiresAt,
    string Email,
    string? DisplayName);

public sealed record MeResponse(
    Guid Id,
    string Email,
    string? DisplayName,
    decimal StartingBalance,
    DateTimeOffset? JournalStartedAt,
    decimal? DefaultRiskPerTrade,
    string TimeZoneId);
