using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using TradeLedger.Core.Analytics;
using TradeLedger.Core.Integrations.Bitunix;
using TradeLedger.Core.Persistence;
using TradeLedger.Core.Shared;

namespace TradeLedger.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTradeLedgerCore(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<BitunixOptions>(configuration.GetSection(BitunixOptions.SectionName));
        services.Configure<EncryptionOptions>(configuration.GetSection(EncryptionOptions.SectionName));

        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres is not configured.");

        services.AddDbContext<TradeLedgerDbContext>(options => options
            .UseNpgsql(connectionString, npgsql => npgsql
                .MigrationsAssembly(typeof(TradeLedgerDbContext).Assembly.FullName))
            .UseSnakeCaseNamingConvention());

        services.AddSingleton<ICredentialProtector, AesGcmCredentialProtector>();

        var redisConnection = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrWhiteSpace(redisConnection))
        {
            services.AddSingleton<IConnectionMultiplexer>(
                _ => ConnectionMultiplexer.Connect(redisConnection));
            services.AddSingleton<IDistributedLock, RedisDistributedLock>();
            services.AddSingleton<IBitunixRateLimiter, RedisBitunixRateLimiter>();
        }
        else
        {
            services.AddSingleton<IDistributedLock, NoOpDistributedLock>();
            services.AddSingleton<IBitunixRateLimiter, NoOpBitunixRateLimiter>();
        }

        services.AddHttpClient<BitunixClient>((provider, http) =>
        {
            var options = provider.GetRequiredService<
                Microsoft.Extensions.Options.IOptions<BitunixOptions>>().Value;
            http.BaseAddress = new Uri(options.BaseUrl);
            http.Timeout = options.HttpTimeout;
        });

        services.AddScoped<BitunixSyncService>();
        services.AddScoped<AnalyticsService>();
        services.AddScoped<PositionSizeCalculator>();

        return services;
    }
}
