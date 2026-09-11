using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TradeLedger.Core.Analytics;
using TradeLedger.Core.Backtesting;
using TradeLedger.Core.Domain;
using TradeLedger.Core.Domain.Backtesting;
using TradeLedger.Core.Persistence;

namespace TradeLedger.Api.Features.Backtests;

public sealed record BacktestAccountResponse(
    Guid Id,
    string Name,
    string? Description,
    decimal StartingBalance,
    decimal CurrentBalance,
    decimal NetProfitLoss,
    string Currency,
    BacktestAccountMode Mode,
    Guid? BacktestStrategyId,
    int SucceededRuns,
    bool IsActive,
    DateTimeOffset CreatedAt)
{
    public static BacktestAccountResponse From(BacktestAccount a, AccountBalance balance) => new(
        a.Id, a.Name, a.Description,
        balance.StartingBalance, balance.CurrentBalance, balance.NetProfitLoss,
        a.Currency, a.Mode, a.BacktestStrategyId, balance.SucceededRuns,
        a.IsActive, a.CreatedAt);
}
