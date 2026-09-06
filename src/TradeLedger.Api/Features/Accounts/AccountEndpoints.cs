using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TradeLedger.Core.Domain;
using TradeLedger.Core.Integrations.Bitunix;
using TradeLedger.Core.Persistence;
using TradeLedger.Core.Shared;

namespace TradeLedger.Api.Features.Accounts;

public static class AccountEndpoints
{
    public static void MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/accounts").WithTags("Accounts").RequireAuthorization();

        group.MapGet("/", async (TradeLedgerDbContext db, CancellationToken ct) =>
        {
            var accounts = await db.Accounts
                .AsNoTracking()
                .Include(a => a.Credential)
                .OrderBy(a => a.Name)
                .Select(a => new AccountResponse(
                    a.Id,
                    a.Name,
                    a.Kind,
                    a.Venue,
                    a.SyncMode,
                    a.QuoteAsset,
                    a.IsActive,
                    a.TrackedFrom,
                    a.Credential != null ? a.Credential.ApiKeyHint : null,
                    a.Credential != null && a.Credential.IsEnabled,
                    a.Credential != null ? a.Credential.LastVerifiedAt : null))
                .ToListAsync(ct);

            return Results.Ok(accounts);
        })
        .WithName("ListAccounts")
        .WithSummary("List accounts")
        .WithDescription("Every venue where value lives: Bitunix futures, Bitunix spot, external wallets and purely manual venues. Credentials are never returned, only the last four characters of the API key so you can tell keys apart.")
        .Produces<List<AccountResponse>>();

        group.MapPost("/", async (
            [FromBody] CreateAccountRequest request,
            TradeLedgerDbContext db,
            CancellationToken ct) =>
        {
            var account = new Account
            {
                Name = request.Name,
                Kind = request.Kind,
                Venue = request.Venue,
                SyncMode = request.Venue == Venue.Manual ? SyncMode.Manual : request.SyncMode,
                QuoteAsset = request.QuoteAsset ?? "USDT",
                TrackedFrom = request.TrackedFrom,
            };

            db.Accounts.Add(account);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/accounts/{account.Id}", new { account.Id });
        })
        .WithName("CreateAccount")
        .WithSummary("Create an account")
        .WithDescription("Registers a venue. Bitunix futures and Bitunix spot are separate accounts. Set trackedFrom to the date the backfill should stop walking back to.")
        .Produces(StatusCodes.Status201Created);

        group.MapPut("/{id:guid}/credentials", async (
            Guid id,
            [FromBody] SetCredentialRequest request,
            TradeLedgerDbContext db,
            ICredentialProtector protector,
            BitunixClient client,
            CancellationToken ct) =>
        {
            var account = await db.Accounts
                .Include(a => a.Credential)
                .FirstOrDefaultAsync(a => a.Id == id, ct);

            if (account is null)
            {
                return Results.NotFound();
            }

            var probe = new BitunixCredentials(
                request.ApiKey, request.ApiSecret, Hint(request.ApiKey));

            var (ok, error) = await client.VerifyCredentialsAsync(probe, ct);
            if (!ok)
            {
                return Results.Problem(
                    title: "Bitunix rejected these credentials",
                    detail: error,
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var credential = account.Credential ?? new ExchangeCredential
            {
                AccountId = account.Id,
                ApiKeyCipher = [],
                ApiSecretCipher = [],
                ApiKeyHint = string.Empty,
            };

            credential.ApiKeyCipher = protector.Protect(request.ApiKey);
            credential.ApiSecretCipher = protector.Protect(request.ApiSecret);
            credential.ApiKeyHint = Hint(request.ApiKey);
            credential.Venue = account.Venue;
            credential.Label = request.Label;
            credential.IsEnabled = true;
            credential.LastVerifiedAt = DateTimeOffset.UtcNow;
            credential.LastVerificationError = null;

            if (account.Credential is null)
            {
                db.ExchangeCredentials.Add(credential);
            }

            account.SyncMode = SyncMode.Api;
            await db.SaveChangesAsync(ct);

            return Results.Ok(new { credential.ApiKeyHint, credential.LastVerifiedAt });
        })
        .WithName("SetAccountCredentials")
        .WithSummary("Attach Bitunix API credentials")
        .WithDescription("Credentials are write-only: they go in and never come back out. The key is verified against Bitunix before it is stored, so a typo fails here rather than silently killing the sync loop later. Stored AES-GCM encrypted, with the encryption key in configuration rather than the database. Use a read-only key: TradeLedger never places orders.")
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}/credentials", async (
            Guid id,
            TradeLedgerDbContext db,
            CancellationToken ct) =>
        {
            var credential = await db.ExchangeCredentials
                .FirstOrDefaultAsync(c => c.AccountId == id, ct);

            if (credential is null)
            {
                return Results.NotFound();
            }

            db.ExchangeCredentials.Remove(credential);
            await db.SaveChangesAsync(ct);

            return Results.NoContent();
        })
        .WithName("DeleteAccountCredentials")
        .WithSummary("Remove Bitunix API credentials")
        .WithDescription("Detaches the key and stops automatic syncing for this account. Trades already imported are kept.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static string Hint(string apiKey) =>
        apiKey.Length <= 4 ? new string('*', apiKey.Length) : apiKey[^4..];
}

public sealed record AccountResponse(
    Guid Id,
    string Name,
    AccountKind Kind,
    Venue Venue,
    SyncMode SyncMode,
    string QuoteAsset,
    bool IsActive,
    DateTimeOffset? TrackedFrom,
    string? ApiKeyHint,
    bool CredentialEnabled,
    DateTimeOffset? LastVerifiedAt);

public sealed record CreateAccountRequest(
    string Name,
    AccountKind Kind,
    Venue Venue,
    SyncMode SyncMode,
    string? QuoteAsset,
    DateTimeOffset? TrackedFrom);

public sealed record SetCredentialRequest(string ApiKey, string ApiSecret, string? Label);
