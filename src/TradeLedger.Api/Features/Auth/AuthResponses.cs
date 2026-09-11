using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TradeLedger.Api.Shared;
using TradeLedger.Core.Domain;

namespace TradeLedger.Api.Features.Auth;

public sealed record LoginResponse(
    string Token,
    DateTimeOffset ExpiresAt,
    string Email,
    string? DisplayName);

public sealed record MeResponse(
    Guid Id,
    string Email,
    string? DisplayName,
    decimal StartingBalance,
    DateTimeOffset? JournalStartedAt,
    decimal? DefaultRiskPerTrade,
    string TimeZoneId);
