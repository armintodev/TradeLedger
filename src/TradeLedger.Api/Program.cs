using System.Text;
using System.Text.Json.Serialization;
using Scalar.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Swashbuckle.AspNetCore.SwaggerUI;
using TradeLedger.Api.Features.Accounts;
using TradeLedger.Api.Features.Analytics;
using TradeLedger.Api.Features.Auth;
using TradeLedger.Api.Features.Backtests;
using TradeLedger.Api.Features.Journal;
using TradeLedger.Api.Features.MarketData;
using TradeLedger.Api.Features.Plans;
using TradeLedger.Api.Features.Portfolio;
using TradeLedger.Api.Features.Proxy;
using TradeLedger.Api.Features.Sync;
using TradeLedger.Api.Features.Trades;
using TradeLedger.Api.Shared;
using TradeLedger.Api.Shared.Errors;
using TradeLedger.Api.Shared.OpenApi;
using TradeLedger.Core;
using TradeLedger.Core.Domain;
using TradeLedger.Core.Persistence;
using TradeLedger.Core.Shared;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<SeedOptions>(builder.Configuration.GetSection(SeedOptions.SectionName));

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUserContext, HttpUserContext>();
builder.Services.AddSingleton<JwtTokenService>();

builder.Services.AddTradeLedgerCore(builder.Configuration);

builder.Services
    .AddIdentityCore<AppUser>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 8;
            options.Password.RequiredUniqueChars = 0;
            options.Password.RequireDigit = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireLowercase = false;
        }
    )
    .AddRoles<AppRole>()
    .AddEntityFrameworkStores<TradeLedgerDbContext>()
    .AddDefaultTokenProviders();

var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwt.Issuer,
                ValidAudience = jwt.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwt.SigningKey ?? new string('0', 32))
                ),
                ClockSkew = TimeSpan.FromMinutes(1),
            };
        }
    );

builder.Services.AddAuthorization();

builder.Services.AddOpenApi(options =>
    {
        options.AddDocumentTransformer<DocumentInfoTransformer>();
        options.AddDocumentTransformer<BearerSecurityTransformer>();
        options.AddOperationTransformer<SecurityRequirementTransformer>();
    }
);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.ConfigureHttpJsonOptions(options =>
    {
        options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    }
);

var app = builder.Build();

app.UseExceptionHandler();

app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi("/openapi/{documentName}.json");

    app.MapScalarApiReference(
        "/docs",
        options => options
            .WithTitle("TradeLedger API")
            .WithTheme(ScalarTheme.BluePlanet)
            .EnableDarkMode()
            .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient)
            .WithOpenApiRoutePattern("/openapi/v1.json")
            .AddPreferredSecuritySchemes("Bearer")
    );

    app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/openapi/v1.json", "TradeLedger API v1");
            options.RoutePrefix = "swagger";
            options.DocumentTitle = "TradeLedger API";
            options.DocExpansion(DocExpansion.List);
            options.DefaultModelsExpandDepth(-1);
            options.DisplayRequestDuration();
            options.EnableTryItOutByDefault();
            options.EnablePersistAuthorization();
            options.EnableFilter();
        }
    );

    app.MapGet("/", () => Results.Redirect("/docs")).ExcludeFromDescription();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();
app.MapAccountEndpoints();
app.MapTradeEndpoints();
app.MapPlanEndpoints();
app.MapTaxonomyEndpoints();
app.MapPortfolioEndpoints();
app.MapProxyEndpoints();
app.MapAnalyticsEndpoints();
app.MapSyncEndpoints();
app.MapMarketDataEndpoints();
app.MapBacktestAccountEndpoints();
app.MapBacktestStrategyEndpoints();
app.MapBacktestRunEndpoints();

app.MapGet("/health", () => Results.Ok(new HealthResponse("ok", DateTimeOffset.UtcNow)))
    .AllowAnonymous()
    .WithTags("Health")
    .WithName("HealthCheck")
    .WithSummary("Liveness probe")
    .WithDescription("Returns ok when the API is up. No authentication required.")
    .Produces<HealthResponse>();

await StartupSeeder.SeedAsync(app.Services);

app.Run();

public partial class Program;

public sealed record HealthResponse(string Status, DateTimeOffset At);
