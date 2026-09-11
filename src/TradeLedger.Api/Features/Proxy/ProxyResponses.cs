using TradeLedger.Core.Shared.Proxy;

namespace TradeLedger.Api.Features.Proxy;

public sealed record ProxyResponse(
    bool Configured,
    bool Enabled,
    ProxyScheme? Scheme,
    string? Host,
    int? Port,
    string? Username,
    bool HasPassword,
    bool Required,
    string? ConfigurationFallback);

public sealed record ProxyUpdatedResponse(string Egress, bool Enabled);
