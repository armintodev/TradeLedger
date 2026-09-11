using System.Globalization;
using System.Net;

namespace TradeLedger.Core.Shared.Proxy;

public enum ProxyScheme
{
    Http = 1,
    Https = 2,
    Socks5 = 3,
    Socks4 = 4,
    Socks4a = 5,
}

public sealed record ProxyEndpoint
{
    public required ProxyScheme Scheme { get; init; }

    public required string Host { get; init; }

    public required int Port { get; init; }

    public string? Username { get; init; }

    public string? Password { get; init; }

    public bool UseCredentials =>
        !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password);

    public string SchemePrefix => Scheme switch
    {
        ProxyScheme.Http => "http",
        ProxyScheme.Https => "https",
        ProxyScheme.Socks5 => "socks5",
        ProxyScheme.Socks4 => "socks4",
        ProxyScheme.Socks4a => "socks4a",
        _ => throw new ArgumentOutOfRangeException(nameof(Scheme), Scheme, "Unsupported proxy scheme."),
    };

    public bool IsSocks =>
        Scheme is ProxyScheme.Socks5 or ProxyScheme.Socks4 or ProxyScheme.Socks4a;

    public Uri ToUri() => new(
        string.Create(CultureInfo.InvariantCulture, $"{SchemePrefix}://{Host}:{Port}"));

    public string CacheKey => string.Create(
        CultureInfo.InvariantCulture,
        $"{SchemePrefix}|{Host}|{Port}|{Username ?? string.Empty}");

    public string Describe() => string.Create(
        CultureInfo.InvariantCulture,
        $"{SchemePrefix}://{(string.IsNullOrWhiteSpace(Username) ? string.Empty : Username + "@")}{Host}:{Port}");

    public IWebProxy ToWebProxy()
    {
        var proxy = new WebProxy(ToUri())
        {
            BypassProxyOnLocal = false,
            UseDefaultCredentials = false,
        };

        if (UseCredentials)
        {
            proxy.Credentials = new NetworkCredential(Username, Password);
        }

        return proxy;
    }

    public static ProxyEndpoint? TryCreate(
        ProxyScheme? scheme,
        string? host,
        int? port,
        string? username,
        string? password)
    {
        if (scheme is null || string.IsNullOrWhiteSpace(host))
        {
            return null;
        }

        if (port is null || port < 1 || port > 65535)
        {
            return null;
        }

        return new ProxyEndpoint
        {
            Scheme = scheme.Value,
            Host = host.Trim(),
            Port = port.Value,
            Username = string.IsNullOrWhiteSpace(username) ? null : username,
            Password = string.IsNullOrWhiteSpace(password) ? null : password,
        };
    }
}

public sealed class ProxyRequiredException(string message) : Exception(message);
