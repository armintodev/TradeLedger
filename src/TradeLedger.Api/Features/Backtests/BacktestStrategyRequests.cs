using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TradeLedger.Core.Backtesting.Indicators;
using TradeLedger.Core.Backtesting.Rules;
using TradeLedger.Core.Domain;
using TradeLedger.Core.Domain.Backtesting;
using TradeLedger.Core.Domain.MarketData;
using TradeLedger.Core.Persistence;

namespace TradeLedger.Api.Features.Backtests;

public sealed record SaveBacktestStrategyRequest(
    string Name,
    JsonElement Rule,
    string? Description,
    Guid? StrategyTermId);

/// <param name="Interval">
/// The interval a run would trade. Optional, and only used to scale the reported warmup:
/// a strategy is authored without knowing what it will run at, so a document whose
/// indicators read a higher timeframe has no single honest bar count without it.
/// </param>
public sealed record ValidateRuleRequest(JsonElement Rule, CandleInterval? Interval);
