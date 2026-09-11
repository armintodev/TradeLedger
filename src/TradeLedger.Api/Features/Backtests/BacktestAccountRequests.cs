using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TradeLedger.Core.Analytics;
using TradeLedger.Core.Backtesting;
using TradeLedger.Core.Domain;
using TradeLedger.Core.Domain.Backtesting;
using TradeLedger.Core.Persistence;

namespace TradeLedger.Api.Features.Backtests;

public sealed record CreateBacktestAccountRequest(
    string Name,
    decimal StartingBalance,
    string? Description,
    string? Currency,
    BacktestAccountMode? Mode,
    Guid? BacktestStrategyId);

public sealed record UpdateBacktestAccountRequest(
    string? Name,
    string? Description,
    BacktestAccountMode? Mode,
    bool? IsActive);
