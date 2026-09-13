using TradeLedger.Core.Domain;

namespace TradeLedger.Api.Features.Auth;

public sealed record LoginRequest(string Email, string Password);

/// <summary>
/// An IANA zone id such as "Asia/Tehran", or "UTC". Display only — no stored instant
/// moves. <see cref="AppUser.SetTimeZone"/> owns the validation.
/// </summary>
public sealed record SetTimeZoneRequest(string TimeZoneId);
