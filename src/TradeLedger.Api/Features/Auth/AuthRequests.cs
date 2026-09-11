using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TradeLedger.Api.Shared;
using TradeLedger.Core.Domain;

namespace TradeLedger.Api.Features.Auth;

public sealed record LoginRequest(string Email, string Password);
