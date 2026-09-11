using Microsoft.Extensions.Options;
using TradeLedger.Core.Shared.Proxy;

namespace TradeLedger.Core.Integrations.Bitunix;

public interface IBitunixHttpClientProvider : IDisposable
{
    HttpClient GetClient(ProxyEndpoint? proxy);

    void Remove(ProxyEndpoint proxy);
}

public sealed class BitunixHttpClientProvider : IBitunixHttpClientProvider
{
    public const string PoolName = "bitunix";

    private readonly IProxiedHttpClientProvider _clients;
    private readonly bool _ownsClients;
    private readonly HttpClientPoolSettings _settings;

    public BitunixHttpClientProvider(
        IProxiedHttpClientProvider clients,
        IOptions<BitunixOptions> bitunixOptions)
        : this(clients, ownsClients: false, bitunixOptions.Value)
    {
    }

    public BitunixHttpClientProvider(
        IOptions<BitunixOptions> bitunixOptions,
        IOptions<ProxyOptions> proxyOptions)
        : this(new ProxiedHttpClientProvider(proxyOptions), ownsClients: true, bitunixOptions.Value)
    {
    }

    private BitunixHttpClientProvider(
        IProxiedHttpClientProvider clients,
        bool ownsClients,
        BitunixOptions options)
    {
        _clients = clients;
        _ownsClients = ownsClients;

        _settings = new HttpClientPoolSettings
        {
            PoolName = PoolName,
            BaseAddress = new Uri(options.BaseUrl),
            Timeout = options.HttpTimeout,
            ConnectTimeout = options.ConnectTimeout,
            MaxConnectionsPerServer = options.MaxConnectionsPerServer,
            PooledConnectionLifetime = options.PooledConnectionLifetime,
            PooledConnectionIdleTimeout = options.PooledConnectionIdleTimeout,
            KeepAlivePingDelay = options.KeepAlivePingDelay,
            KeepAlivePingTimeout = options.KeepAlivePingTimeout,
        };
    }

    public HttpClient GetClient(ProxyEndpoint? proxy) => _clients.GetClient(_settings, proxy);

    public void Remove(ProxyEndpoint proxy) => _clients.Remove(proxy);

    public void Dispose()
    {
        if (_ownsClients)
        {
            _clients.Dispose();
        }
    }
}
