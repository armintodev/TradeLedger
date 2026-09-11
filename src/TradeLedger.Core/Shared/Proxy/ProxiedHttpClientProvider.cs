using System.Collections.Concurrent;
using System.Net;
using Microsoft.Extensions.Options;

namespace TradeLedger.Core.Shared.Proxy;

public sealed record HttpClientPoolSettings
{
    public required string PoolName { get; init; }
    public required Uri BaseAddress { get; init; }
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(15);
    public int MaxConnectionsPerServer { get; init; } = 10;
    public TimeSpan PooledConnectionLifetime { get; init; } = TimeSpan.FromMinutes(10);
    public TimeSpan PooledConnectionIdleTimeout { get; init; } = TimeSpan.FromMinutes(2);
    public TimeSpan KeepAlivePingDelay { get; init; } = TimeSpan.FromMinutes(1);
    public TimeSpan KeepAlivePingTimeout { get; init; } = TimeSpan.FromSeconds(30);
}

public interface IProxiedHttpClientProvider : IDisposable
{
    HttpClient GetClient(HttpClientPoolSettings settings, ProxyEndpoint? proxy);

    void Remove(ProxyEndpoint proxy);
}

public sealed class ProxiedHttpClientProvider : IProxiedHttpClientProvider
{
    private const string DirectKey = " direct";

    private readonly ConcurrentDictionary<string, HttpClient> _clients = new(StringComparer.Ordinal);
    private readonly ProxyOptions _proxy;
    private bool _disposed;

    public ProxiedHttpClientProvider(IOptions<ProxyOptions> proxyOptions)
    {
        _proxy = proxyOptions.Value;
    }

    public HttpClient GetClient(HttpClientPoolSettings settings, ProxyEndpoint? proxy)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (proxy is null && _proxy.Required)
        {
            throw new ProxyRequiredException(
                "No proxy is configured for this user and Proxy:Required is true, so the request was " +
                "refused rather than sent from the host address. Configure a proxy via PUT /api/proxy " +
                "or the Proxy section in configuration.");
        }

        return _clients.GetOrAdd(KeyFor(settings.PoolName, proxy), _ => Create(settings, proxy));
    }

    public void Remove(ProxyEndpoint proxy)
    {
        var suffix = "|" + proxy.CacheKey;

        foreach (var key in _clients.Keys.Where(k => k.EndsWith(suffix, StringComparison.Ordinal)))
        {
            if (_clients.TryRemove(key, out var client))
            {
                client.Dispose();
            }
        }
    }

    private static string KeyFor(string poolName, ProxyEndpoint? proxy) =>
        poolName + "|" + (proxy?.CacheKey ?? DirectKey);

    private static HttpClient Create(HttpClientPoolSettings settings, ProxyEndpoint? proxy)
    {
        var handler = new SocketsHttpHandler
        {
            MaxConnectionsPerServer = settings.MaxConnectionsPerServer,
            PooledConnectionLifetime = settings.PooledConnectionLifetime,
            PooledConnectionIdleTimeout = settings.PooledConnectionIdleTimeout,
            ConnectTimeout = settings.ConnectTimeout,
            KeepAlivePingDelay = settings.KeepAlivePingDelay,
            KeepAlivePingTimeout = settings.KeepAlivePingTimeout,
            KeepAlivePingPolicy = HttpKeepAlivePingPolicy.WithActiveRequests,
            AutomaticDecompression = DecompressionMethods.All,
        };

        if (proxy is not null)
        {
            var webProxy = proxy.ToWebProxy();

            handler.Proxy = webProxy;
            handler.UseProxy = true;

            if (proxy.UseCredentials)
            {
                handler.DefaultProxyCredentials = webProxy.Credentials;
            }
        }
        else
        {
            handler.UseProxy = false;
        }

        return new HttpClient(handler, disposeHandler: true)
        {
            BaseAddress = settings.BaseAddress,
            Timeout = settings.Timeout,
        };
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (var entry in _clients)
        {
            entry.Value.Dispose();
        }

        _clients.Clear();
    }
}
