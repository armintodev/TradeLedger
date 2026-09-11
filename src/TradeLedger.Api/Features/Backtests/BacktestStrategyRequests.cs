using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TradeLedger.Core.Backtesting.Indicators;
using TradeLedger.Core.Backtesting.Rules;
using TradeLedger.Core.Domain;
using TradeLedger.Core.Domain.Backtesting;
using TradeLedger.Core.Persistence;

namespace TradeLedger.Api.Features.Backtests;

public sealed record SaveBacktestStrategyRequest(
    string Name,
    JsonElement Rule,
    string? Description,
    Guid? StrategyTermId);

public sealed record ValidateRuleRequest(JsonElement Rule);
