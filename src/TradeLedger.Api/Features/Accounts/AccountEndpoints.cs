using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TradeLedger.Core.Domain;
using TradeLedger.Core.Integrations.Bitunix;
using TradeLedger.Core.Persistence;
using TradeLedger.Core.Shared;
using TradeLedger.Core.Shared.Proxy;

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
                    a.Credential != null ? a.Credential.LastVerifiedAt : null,
                    a.Credential != null ? a.Credential.VerifiedViaEgress : null))
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
            var account = Account.Create(
                request.Name,
                request.Kind,
                request.Venue,
                request.SyncMode,
                request.QuoteAsset,
                request.TrackedFrom);

            db.Accounts.Add(account);
            await db.SaveChangesAsync(ct);

            return Results.Created(
                $"/api/accounts/{account.Id}",
                new CreatedAccountResponse(account.Id));
        })
        .WithName("CreateAccount")
        .WithSummary("Create an account")
        .WithDescription("Registers a venue. Bitunix futures and Bitunix spot are separate accounts. Set trackedFrom to the date the backfill should stop walking back to.")
        .Produces<CreatedAccountResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPut("/{id:guid}/credentials", async (
            Guid id,
            [FromBody] SetCredentialRequest request,
            TradeLedgerDbContext db,
            ICredentialProtector protector,
            BitunixClient client,
            IUserProxyResolver proxyResolver,
            CancellationToken ct) =>
        {
            var account = await db.Accounts
                .Include(a => a.Credential)
                .FirstOrDefaultAsync(a => a.Id == id, ct)
                ?? throw new ResourceNotFoundException("Account", id);

            var proxy = await proxyResolver.ResolveAsync(account.UserId, ct);

            var probe = new BitunixConnection(
                new BitunixCredentials(request.ApiKey, request.ApiSecret, Hint(request.ApiKey)),
                proxy);

            var (ok, error) = await client.VerifyCredentialsAsync(probe, ct);

            if (!ok)
            {
                throw new DomainRuleException(
                    "credential_rejected",
                    error ?? "Bitunix rejected these credentials.");
            }

            var isNew = account.Credential is null;

            var credential = account.AttachCredential(
                protector.Protect(request.ApiKey),
                protector.Protect(request.ApiSecret),
                Hint(request.ApiKey),
                request.Label,
                probe.DescribeEgress());

            if (isNew)
            {
                db.ExchangeCredentials.Add(credential);
            }

            await db.SaveChangesAsync(ct);

            return Results.Ok(new CredentialResponse(
                credential.ApiKeyHint,
                credential.LastVerifiedAt,
                credential.VerifiedViaEgress));
        })
        .WithName("SetAccountCredentials")
        .WithSummary("Attach Bitunix API credentials")
        .WithDescription("Credentials are write-only: they go in and never come back out. The key is verified against Bitunix before it is stored, so a typo fails here rather than silently killing the sync loop later. Stored AES-GCM encrypted, with the encryption key in configuration rather than the database. Use a read-only key: TradeLedger never places orders.")
        .Produces<CredentialResponse>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapDelete("/{id:guid}/credentials", async (
            Guid id,
            TradeLedgerDbContext db,
            CancellationToken ct) =>
        {
            var account = await db.Accounts
                .Include(a => a.Credential)
                .FirstOrDefaultAsync(a => a.Id == id, ct)
                ?? throw new ResourceNotFoundException("Account", id);

            if (account.Credential is null)
            {
                throw new ResourceNotFoundException("Credential for account", id);
            }

            db.ExchangeCredentials.Remove(account.Credential);
            account.DetachCredential();

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
