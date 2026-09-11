using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TradeLedger.Core.Domain.MarketData;
using TradeLedger.Core.MarketData;
using TradeLedger.Core.Persistence;
using TradeLedger.Core.Shared;

namespace TradeLedger.Api.Features.MarketData;

public sealed record GapQuery(
    [FromQuery] CandleSource Source,
    [FromQuery] string Symbol,
    [FromQuery] CandleInterval Interval,
    [FromQuery] DateTimeOffset From,
    [FromQuery] DateTimeOffset To);

public sealed record BackfillRequest(
    CandleSource Source,
    string Symbol,
    CandleInterval Interval,
    DateTimeOffset From,
    DateTimeOffset To,
    bool? IncludeFundingRates);
