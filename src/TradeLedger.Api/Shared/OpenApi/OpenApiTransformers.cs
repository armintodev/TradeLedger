using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace TradeLedger.Api.Shared.OpenApi;

public sealed class DocumentInfoTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        document.Info = new OpenApiInfo
        {
            Title = "TradeLedger API",
            Version = "v1",
            Description = Description,
            Contact = new OpenApiContact { Name = "TradeLedger" },
        };

        document.Tags = OpenApiTags.All;

        return Task.CompletedTask;
    }

    private const string Description = """
        Personal crypto trade journal and portfolio dashboard, rebuilt from the
        owner's Excel workbook and synced automatically from the Bitunix exchange.

        **Read-only against the exchange.** TradeLedger never places, modifies or
        cancels an order. It is a journal, not a trading bot.

        ### How a trade reaches the journal

        A closed Bitunix position becomes a journal row on its own. The mechanical
        half (prices, quantity, leverage, fees, funding, PnL) comes from the
        exchange; the subjective half (strategy, mental state, mistakes, rating,
        screenshots) is supplied by the trader through `PATCH /api/trades/{id}/journal`.

        Trades that had a `TradePlan` before execution are linked and flagged
        `isPlanned`; the rest are flagged unplanned, which is itself a discipline metric.

        ### Getting a token

        Call `POST /api/auth/login`, then click **Authorize** and paste the token.
        Public signup is deliberately absent; the owner account is seeded at startup.
        """;
}

public sealed class BearerSecurityTransformer : IOpenApiDocumentTransformer
{
    private const string SchemeName = "Bearer";

    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        var scheme = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description =
                "JWT from POST /api/auth/login. Paste the raw token; the 'Bearer ' prefix is added for you.",
        };

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[SchemeName] = scheme;

        return Task.CompletedTask;
    }
}

public sealed class SecurityRequirementTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        var requiresAuth = context.Description.ActionDescriptor.EndpointMetadata
            .OfType<Microsoft.AspNetCore.Authorization.IAuthorizeData>()
            .Any();

        var allowsAnonymous = context.Description.ActionDescriptor.EndpointMetadata
            .OfType<Microsoft.AspNetCore.Authorization.IAllowAnonymous>()
            .Any();

        if (!requiresAuth || allowsAnonymous)
        {
            return Task.CompletedTask;
        }

        operation.Security =
        [
            new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", context.Document)] = []
            }
        ];

        operation.Responses ??= new OpenApiResponses();
        operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Missing or expired token." });

        return Task.CompletedTask;
    }
}

public static class OpenApiTags
{
    public static HashSet<OpenApiTag> All =>
    [
        Tag("Auth", "Sign in and inspect the current user."),
        Tag("Accounts", "Venues where value lives, and their encrypted exchange credentials."),
        Tag("Trades", "The journal itself: synced and manual trades, plus the review inbox."),
        Tag("Plans", "Pre-trade plans and the position-size calculator."),
        Tag("Journal", "The vocabularies behind the journal form: strategies, mental states, mistakes."),
        Tag("Portfolio", "Holdings, LP and farm positions, transfers, and manual balance snapshots."),
        Tag("Analytics", "Performance metrics, equity curve, drawdown and breakdowns."),
        Tag("Sync", "Bitunix sync status, manual triggers and historical backfill."),
        Tag("Proxy", "The egress proxy every exchange request is routed through, so the exchange always sees one static IP."),
        Tag("Backtests", "Simulated accounts, runs and trades. Entirely separate from the live journal."),
        Tag("Market Data", "Historical candles behind backtesting: Binance backfill, CSV import, coverage and gaps."),
        Tag("Health", "Liveness probe."),
    ];

    private static OpenApiTag Tag(string name, string description) =>
        new() { Name = name, Description = description };
}
