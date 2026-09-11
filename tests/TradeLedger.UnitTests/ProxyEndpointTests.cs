using System.Net;
using TradeLedger.Core.Shared.Proxy;

namespace TradeLedger.UnitTests;

public class ProxyEndpointTests
{
    [Theory]
    [InlineData(ProxyScheme.Http, "http://proxy.example.com:8080/")]
    [InlineData(ProxyScheme.Https, "https://proxy.example.com:8080/")]
    [InlineData(ProxyScheme.Socks5, "socks5://proxy.example.com:8080/")]
    [InlineData(ProxyScheme.Socks4, "socks4://proxy.example.com:8080/")]
    [InlineData(ProxyScheme.Socks4a, "socks4a://proxy.example.com:8080/")]
    public void ToUri_CarriesTheSchemeSoSocksActuallyEngages(ProxyScheme scheme, string expected)
    {
        var endpoint = new ProxyEndpoint { Scheme = scheme, Host = "proxy.example.com", Port = 8080 };

        Assert.Equal(expected, endpoint.ToUri().ToString());
    }

    [Fact]
    public void ToWebProxy_ResolvesThroughTheProxyForExchangeTraffic()
    {
        var endpoint = new ProxyEndpoint { Scheme = ProxyScheme.Socks5, Host = "10.0.0.5", Port = 1080 };
        var target = new Uri("https://fapi.bitunix.com/api/v1/futures/account");

        var proxy = endpoint.ToWebProxy();

        Assert.Equal("socks5://10.0.0.5:1080/", proxy.GetProxy(target)!.ToString());
        Assert.False(proxy.IsBypassed(target));
    }

    [Fact]
    public void ToWebProxy_NeverBypassesForLocalTargets()
    {
        var endpoint = new ProxyEndpoint { Scheme = ProxyScheme.Http, Host = "10.0.0.5", Port = 8080 };

        var proxy = (WebProxy)endpoint.ToWebProxy();

        Assert.False(proxy.BypassProxyOnLocal);
        Assert.False(proxy.UseDefaultCredentials);
    }

    [Fact]
    public void ToWebProxy_AttachesCredentialsWhenSupplied()
    {
        var endpoint = new ProxyEndpoint
        {
            Scheme = ProxyScheme.Socks5,
            Host = "10.0.0.5",
            Port = 1080,
            Username = "tunnel",
            Password = "secret",
        };

        var proxy = endpoint.ToWebProxy();
        var credentials = proxy.Credentials;

        Assert.NotNull(credentials);
        var credential = credentials.GetCredential(endpoint.ToUri(), "Basic");
        Assert.NotNull(credential);

        Assert.True(endpoint.UseCredentials);
        Assert.Equal("tunnel", credential.UserName);
        Assert.Equal("secret", credential.Password);
    }

    [Fact]
    public void ToWebProxy_LeavesCredentialsUnsetWhenOnlyAUsernameIsGiven()
    {
        var endpoint = new ProxyEndpoint
        {
            Scheme = ProxyScheme.Socks5,
            Host = "10.0.0.5",
            Port = 1080,
            Username = "tunnel",
        };

        Assert.False(endpoint.UseCredentials);
        Assert.Null(endpoint.ToWebProxy().Credentials);
    }

    [Fact]
    public void Describe_NeverLeaksThePassword()
    {
        var endpoint = new ProxyEndpoint
        {
            Scheme = ProxyScheme.Socks5,
            Host = "10.0.0.5",
            Port = 1080,
            Username = "tunnel",
            Password = "super-secret",
        };

        var described = endpoint.Describe();

        Assert.DoesNotContain("super-secret", described, StringComparison.Ordinal);
        Assert.Equal("socks5://tunnel@10.0.0.5:1080", described);
    }

    [Fact]
    public void CacheKey_IsStableForTheSameProxyAndIgnoresThePassword()
    {
        var a = new ProxyEndpoint
        {
            Scheme = ProxyScheme.Socks5, Host = "10.0.0.5", Port = 1080,
            Username = "tunnel", Password = "one",
        };

        var b = a with { Password = "two" };

        Assert.Equal(a.CacheKey, b.CacheKey);
    }

    [Theory]
    [InlineData(ProxyScheme.Http, "10.0.0.5", 8080, null)]
    [InlineData(ProxyScheme.Socks5, "10.0.0.6", 1080, null)]
    [InlineData(ProxyScheme.Socks5, "10.0.0.5", 1081, null)]
    [InlineData(ProxyScheme.Socks5, "10.0.0.5", 1080, "other")]
    public void CacheKey_DistinguishesDifferentProxies(
        ProxyScheme scheme, string host, int port, string? username)
    {
        var baseline = new ProxyEndpoint
        {
            Scheme = ProxyScheme.Socks5, Host = "10.0.0.5", Port = 1080, Username = "tunnel",
        };

        var other = new ProxyEndpoint
        {
            Scheme = scheme, Host = host, Port = port, Username = username ?? "tunnel",
        };

        Assert.NotEqual(baseline.CacheKey, other.CacheKey);
    }

    [Theory]
    [InlineData(null, "10.0.0.5", 1080)]
    [InlineData(ProxyScheme.Socks5, null, 1080)]
    [InlineData(ProxyScheme.Socks5, "   ", 1080)]
    [InlineData(ProxyScheme.Socks5, "10.0.0.5", 0)]
    [InlineData(ProxyScheme.Socks5, "10.0.0.5", -1)]
    [InlineData(ProxyScheme.Socks5, "10.0.0.5", 70000)]
    public void TryCreate_RejectsIncompleteConfiguration(
        ProxyScheme? scheme, string? host, int port)
    {
        Assert.Null(ProxyEndpoint.TryCreate(scheme, host, port, null, null));
    }

    [Fact]
    public void TryCreate_BuildsAnEndpointFromCompleteConfiguration()
    {
        var endpoint = ProxyEndpoint.TryCreate(
            ProxyScheme.Socks5, "  10.0.0.5  ", 1080, "tunnel", "secret");

        Assert.NotNull(endpoint);
        Assert.Equal("10.0.0.5", endpoint.Host);
        Assert.True(endpoint.IsSocks);
    }

    [Fact]
    public void IsSocks_IsFalseForHttpProxies()
    {
        var http = new ProxyEndpoint { Scheme = ProxyScheme.Http, Host = "h", Port = 1 };

        Assert.False(http.IsSocks);
    }
}
