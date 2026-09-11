using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TradeLedger.Core.Analytics;
using TradeLedger.Core.Backtesting;
using TradeLedger.Core.Domain;
using TradeLedger.Core.Domain.Backtesting;
using TradeLedger.Core.Domain.MarketData;
using TradeLedger.Core.MarketData;
using TradeLedger.Core.Persistence;
using TradeLedger.Core.Shared;

namespace TradeLedger.Api.Features.Backtests;

public sealed record QueueBacktestRequest(
    Guid BacktestAccountId,
    DateTimeOffset From,
    DateTimeOffset To,
    Guid? BacktestStrategyId,
    string? Symbol,
    CandleSource? Source,
    CandleInterval? Interval,
    decimal? RiskPercentPerPosition,
    decimal? RiskRewardRatio,
    int? Leverage,
    decimal? TakerFeeRate,
    decimal? MakerFeeRate,
    decimal? SlippageRate,
    decimal? MaintenanceMarginRate,
    bool? IncludeFunding,
    bool? AllowGaps);
