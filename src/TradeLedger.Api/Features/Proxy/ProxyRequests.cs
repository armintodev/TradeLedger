using TradeLedger.Core.Shared.Proxy;

namespace TradeLedger.Api.Features.Proxy;

public sealed record SetProxyRequest(
    ProxyScheme Scheme,
    string Host,
    int Port,
    string? Username,
    string? Password,
    bool? Enabled,
    bool? ClearPassword);
