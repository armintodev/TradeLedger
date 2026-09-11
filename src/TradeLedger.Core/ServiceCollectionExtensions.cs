using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using TradeLedger.Core.Analytics;
using TradeLedger.Core.Backtesting;
using TradeLedger.Core.Backtesting.Engine;
using TradeLedger.Core.Domain.MarketData;
using TradeLedger.Core.Integrations.Bitunix;
using TradeLedger.Core.MarketData;
using TradeLedger.Core.Persistence;
using TradeLedger.Core.Shared;
using TradeLedger.Core.Shared.Proxy;

namespace TradeLedger.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTradeLedgerCore(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<BitunixOptions>(configuration.GetSection(BitunixOptions.SectionName));
        services.Configure<EncryptionOptions>(configuration.GetSection(EncryptionOptions.SectionName));
        services.Configure<ProxyOptions>(configuration.GetSection(ProxyOptions.SectionName));
        services.Configure<MarketDataOptions>(configuration.GetSection(MarketDataOptions.SectionName));

        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres is not configured.");

        services.AddDbContext<TradeLedgerDbContext>(options => options
            .UseNpgsql(connectionString, npgsql => npgsql
                .MigrationsAssembly(typeof(TradeLedgerDbContext).Assembly.FullName))
            .UseSnakeCaseNamingConvention());

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddSingleton<ICredentialProtector, AesGcmCredentialProtector>();

        var redisConnection = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrWhiteSpace(redisConnection))
        {
            services.AddSingleton<IConnectionMultiplexer>(
                _ => ConnectionMultiplexer.Connect(redisConnection));
            services.AddSingleton<IDistributedLock, RedisDistributedLock>();
            services.AddSingleton<IBitunixRateLimiter, RedisBitunixRateLimiter>();
            services.AddSingleton<IKlineRateLimiter, RedisKlineRateLimiter>();
        }
        else
        {
            services.AddSingleton<IDistributedLock, NoOpDistributedLock>();
            services.AddSingleton<IBitunixRateLimiter, NoOpBitunixRateLimiter>();
            services.AddSingleton<IKlineRateLimiter, NoOpKlineRateLimiter>();
        }

        services.AddSingleton<IProxiedHttpClientProvider, ProxiedHttpClientProvider>();

        services.AddSingleton<IBitunixHttpClientProvider>(sp => new BitunixHttpClientProvider(
            sp.GetRequiredService<IProxiedHttpClientProvider>(),
            sp.GetRequiredService<IOptions<BitunixOptions>>()));

        services.AddScoped<IUserProxyResolver, UserProxyResolver>();
        services.AddScoped<BitunixClient>();

        services.AddScoped<BitunixSyncService>();
        services.AddScoped<AnalyticsService>();
        services.AddScoped<PositionSizeCalculator>();

        services.AddOptions<BacktestOptions>()
            .Bind(configuration.GetSection(BacktestOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<BacktestOptions>, BacktestOptionsValidator>();

        services.AddScoped<BacktestAccountService>();
        services.AddScoped<BacktestRunner>();
        services.AddScoped<IBacktestEngine, RuleEngineSimulator>();

        services.AddScoped<CandleRepository>();
        services.AddScoped<IKlineSource, BinanceKlineClient>();
        services.AddScoped<MarketDataBackfillService>();

        return services;
    }
}
