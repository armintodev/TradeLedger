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
    IReadOnlyList<string> Warnings,
    JsonElement? Rule)
{
    /// The frozen rule travels with the by-id fetch only, the way
    /// BacktestStrategyResponse gates its own: a list of fifty runs should not carry
    /// fifty rule trees, but a single result is unexplainable without the one that
    /// produced it, and the live strategy may be several versions ahead by then.
    public static BacktestRunResponse Of(BacktestRun r, bool includeRule = false) => new(
        r.Id, r.BacktestAccountId, r.Kind, r.Status, r.BacktestStrategyId, r.RuleHash,
        r.Symbol, r.Source, r.Interval, r.From, r.To,
        r.OpeningBalance, r.ClosingBalance,
        r.RiskPercentPerPosition, r.RiskRewardRatio, r.Leverage,
        r.TakerFeeRate, r.SlippageRate, r.MaintenanceMarginRate, r.IncludeFunding,
        r.AllowGaps, r.DataQuality, r.EngineVersion,
        r.TotalBars, r.BarsProcessed, r.ProgressPercent, r.CancellationRequested,
        r.QueuedAt, r.StartedAt, r.FinishedAt, r.Error,
        BacktestRunEndpoints.DeserializeSummary(r.ResultJson),
        BacktestRunEndpoints.DeserializeWarnings(r.WarningsJson),
        includeRule && r.RuleJson is { } json
            ? JsonDocument.Parse(json).RootElement.Clone()
            : null);
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


/// The list row plus the parts a ledger view has no room for: the bar indices the
/// position occupied, the engine's own note, and the fills themselves. Folding the
/// executions in here rather than leaving them a second round trip is the whole
/// point of a by-id fetch — a position is explained by when it was entered and on
/// what, not by a row of totals.
public sealed record BacktestTradeDetailResponse(
    Guid Id,
    Guid BacktestRunId,
    int Sequence,
    string Symbol,
    TradeSide Side,
    DateTimeOffset OpenedAt,
    DateTimeOffset? ClosedAt,
    int EntryBarIndex,
    int ExitBarIndex,
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
    bool ExitWasAssumed,
    bool WasLiquidated,
    decimal? MaeR,
    decimal? MfeR,
    Guid? SourceTradeId,
    string? Notes,
    IReadOnlyList<BacktestExecutionResponse> Executions)
{
    public static BacktestTradeDetailResponse From(BacktestTrade t) => new(
        t.Id, t.BacktestRunId, t.Sequence, t.Symbol, t.Side, t.OpenedAt, t.ClosedAt,
        t.EntryBarIndex, t.ExitBarIndex, t.BarsInTrade,
        t.EntryPrice, t.ExitPrice, t.Quantity, t.Leverage, t.PositionMargin, t.OrderValue,
        t.StopLossPrice, t.TakeProfitPrice, t.LiquidationPrice,
        t.GrossProfitLoss, t.Fees, t.Funding, t.NetProfitLoss,
        t.AchievedReturnR, t.PlannedReturnR, t.TradeGainPercent, t.BalanceAfter,
        t.Duration, t.Outcome, t.ExitReason, t.IntrabarResolution, t.ExitWasAssumed,
        t.WasLiquidated, t.MaeR, t.MfeR, t.SourceTradeId, t.Notes,
        [.. t.Executions
            .OrderBy(e => e.ExecutedAt)
            .ThenBy(e => e.BarIndex)
            .Select(BacktestExecutionResponse.From)]);
}
public sealed record BacktestExecutionResponse(
    Guid Id,
    Guid BacktestTradeId,
    ExecutionRole Role,
    decimal Price,
    decimal Quantity,
    decimal Fee,
    decimal Notional,
    DateTimeOffset ExecutedAt,
    int BarIndex)
{
    public static BacktestExecutionResponse From(BacktestExecution e) => new(
        e.Id, e.BacktestTradeId, e.Role, e.Price, e.Quantity, e.Fee, e.Notional,
        e.ExecutedAt, e.BarIndex);
}
