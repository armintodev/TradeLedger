using Microsoft.Extensions.Options;
using TradeLedger.Core.Integrations.Bitunix;
using TradeLedger.Core.Shared.Proxy;

namespace TradeLedger.UnitTests;

public class BitunixHttpClientProviderTests
{
    [Fact]
    public void GetClient_ReusesOneClientPerProxy()
    {
        using var provider = Create();
        var proxy = Socks("10.0.0.5", 1080);

        Assert.Same(provider.GetClient(proxy), provider.GetClient(proxy with { }));
    }

    [Fact]
    public void GetClient_IsolatesDifferentProxies()
    {
        using var provider = Create();

        var first = provider.GetClient(Socks("10.0.0.5", 1080));
        var second = provider.GetClient(Socks("10.0.0.6", 1080));

        Assert.NotSame(first, second);
    }

    [Fact]
    public void GetClient_RefusesDirectTrafficWhenProxyIsRequired()
    {
        using var provider = Create(required: true);

        Assert.Throws<ProxyRequiredException>(() => provider.GetClient(null));
    }

    [Fact]
    public void GetClient_AllowsDirectTrafficWhenProxyIsNotRequired()
    {
        using var provider = Create(required: false);

        var client = provider.GetClient(null);

        Assert.Equal(new Uri("https://fapi.bitunix.com"), client.BaseAddress);
    }

    [Fact]
    public void GetClient_AppliesBaseAddressAndTimeout()
    {
        using var provider = Create();

        var client = provider.GetClient(Socks("10.0.0.5", 1080));

        Assert.Equal(new Uri("https://fapi.bitunix.com"), client.BaseAddress);
        Assert.Equal(TimeSpan.FromSeconds(30), client.Timeout);
    }

    [Fact]
    public void Remove_DropsTheCachedClientSoTheNextCallRebuildsIt()
    {
        using var provider = Create();
        var proxy = Socks("10.0.0.5", 1080);

        var first = provider.GetClient(proxy);
        provider.Remove(proxy);
        var second = provider.GetClient(proxy);

        Assert.NotSame(first, second);
    }

    [Fact]
    public void GetClient_ThrowsAfterDispose()
    {
        var provider = Create();
        provider.Dispose();

        Assert.Throws<ObjectDisposedException>(() => provider.GetClient(Socks("10.0.0.5", 1080)));
    }

    private static ProxyEndpoint Socks(string host, int port) => new()
    {
        Scheme = ProxyScheme.Socks5,
        Host = host,
        Port = port,
        Username = "tunnel",
        Password = "secret",
    };

    private static BitunixHttpClientProvider Create(bool required = true) => new(
        Options.Create(new BitunixOptions()),
        Options.Create(new ProxyOptions { Required = required }));
}
