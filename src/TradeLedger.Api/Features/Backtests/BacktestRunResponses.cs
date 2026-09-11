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

public sealed record BacktestRunResponse(
    Guid Id,
    Guid BacktestAccountId,
    BacktestKind Kind,
    BacktestStatus Status,
    Guid? BacktestStrategyId,
    string? RuleHash,
    string? Symbol,
    CandleSource? Source,
    CandleInterval? Interval,
    DateTimeOffset From,
    DateTimeOffset To,
    decimal OpeningBalance,
    decimal? ClosingBalance,
    decimal RiskPercentPerPosition,
    decimal RiskRewardRatio,
    int Leverage,
    decimal TakerFeeRate,
    decimal SlippageRate,
    decimal MaintenanceMarginRate,
    bool IncludeFunding,
    bool AllowGaps,
    DataQuality DataQuality,
    int EngineVersion,
    int TotalBars,
    int BarsProcessed,
    decimal ProgressPercent,
    bool CancellationRequested,
    DateTimeOffset QueuedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    string? Error,
    BacktestResultSummary? Result,
    IReadOnlyList<string> Warnings)
{
    public static BacktestRunResponse Of(BacktestRun r) => new(
        r.Id, r.BacktestAccountId, r.Kind, r.Status, r.BacktestStrategyId, r.RuleHash,
        r.Symbol, r.Source, r.Interval, r.From, r.To,
        r.OpeningBalance, r.ClosingBalance,
        r.RiskPercentPerPosition, r.RiskRewardRatio, r.Leverage,
        r.TakerFeeRate, r.SlippageRate, r.MaintenanceMarginRate, r.IncludeFunding,
        r.AllowGaps, r.DataQuality, r.EngineVersion,
        r.TotalBars, r.BarsProcessed, r.ProgressPercent, r.CancellationRequested,
        r.QueuedAt, r.StartedAt, r.FinishedAt, r.Error,
        BacktestRunEndpoints.DeserializeSummary(r.ResultJson),
        BacktestRunEndpoints.DeserializeWarnings(r.WarningsJson));
}

public sealed record BacktestTradeResponse(
    Guid Id,
    int Sequence,
    string Symbol,
    TradeSide Side,
    DateTimeOffset OpenedAt,
    DateTimeOffset? ClosedAt,
    int BarsInTrade,
    decimal EntryPrice,
    decimal? ExitPrice,
    decimal Quantity,
    int Leverage,
    decimal PositionMargin,
    decimal OrderValue,
    decimal StopLossPrice,
    decimal TakeProfitPrice,
    decimal LiquidationPrice,
    decimal GrossProfitLoss,
    decimal Fees,
    decimal Funding,
    decimal NetProfitLoss,
    decimal? AchievedReturnR,
    decimal? PlannedReturnR,
    decimal? TradeGainPercent,
    decimal BalanceAfter,
    TimeSpan? Duration,
    TradeOutcome Outcome,
    BacktestExitReason? ExitReason,
    IntrabarResolution IntrabarResolution,
    bool WasLiquidated,
    decimal? MaeR,
    decimal? MfeR,
    Guid? SourceTradeId)
{
    public static BacktestTradeResponse From(BacktestTrade t) => new(
        t.Id, t.Sequence, t.Symbol, t.Side, t.OpenedAt, t.ClosedAt, t.BarsInTrade,
        t.EntryPrice, t.ExitPrice, t.Quantity, t.Leverage, t.PositionMargin, t.OrderValue,
        t.StopLossPrice, t.TakeProfitPrice, t.LiquidationPrice,
        t.GrossProfitLoss, t.Fees, t.Funding, t.NetProfitLoss,
        t.AchievedReturnR, t.PlannedReturnR, t.TradeGainPercent, t.BalanceAfter,
        t.Duration, t.Outcome, t.ExitReason, t.IntrabarResolution, t.WasLiquidated,
        t.MaeR, t.MfeR, t.SourceTradeId);
}
