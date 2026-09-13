using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TradeLedger.Api.Shared;
using TradeLedger.Core.Analytics;
using TradeLedger.Core.Backtesting;
using TradeLedger.Core.Domain;
using TradeLedger.Core.Domain.Backtesting;
using TradeLedger.Core.Domain.MarketData;
using TradeLedger.Core.MarketData;
using TradeLedger.Core.Persistence;
using TradeLedger.Core.Shared;

namespace TradeLedger.Api.Features.Backtests;

public static class BacktestRunEndpoints
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static void MapBacktestRunEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/backtests")
            .WithTags("Backtests")
            .RequireAuthorization();

        group.MapGet("/", async (
            TradeLedgerDbContext db,
            [FromQuery] Guid? accountId,
            [FromQuery] BacktestKind? kind,
            [FromQuery] BacktestStatus? status,
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            CancellationToken ct) =>
        {
            var take = Math.Clamp(pageSize ?? 50, 1, 200);
            var skip = Math.Max(page ?? 1, 1) - 1;

            var query = db.BacktestRuns.AsNoTracking();

            if (accountId is { } account)
            {
                query = query.Where(r => r.BacktestAccountId == account);
            }

            if (kind is { } k)
            {
                query = query.Where(r => r.Kind == k);
            }

            if (status is { } s)
            {
                query = query.Where(r => r.Status == s);
            }

            var total = await query.CountAsync(ct);

            var runs = await query
                .OrderByDescending(r => r.QueuedAt)
                .Skip(skip * take)
                .Take(take)
                .ToListAsync(ct);

            return Results.Ok(new PagedResult<BacktestRunResponse>(
                [.. runs.Select(run => BacktestRunResponse.Of(run))],
                skip + 1,
                take,
                total));
        })
        .WithName("ListBacktestRuns")
        .WithSummary("List backtest runs")
        .WithDescription("Newest first, filterable by account, kind and status. Page defaults to 1 and pageSize to 50, and the response carries the total so a frontend can page without guessing.")
        .Produces<PagedResult<BacktestRunResponse>>();

        group.MapPost("/", async (
            [FromBody] QueueBacktestRequest request,
            TradeLedgerDbContext db,
            BacktestAccountService accounts,
            BacktestRunner runner,
            CandleRepository candles,
            IOptions<BacktestOptions> options,
            IUserContext user,
            CancellationToken ct) =>
            await QueueAsync(
                request, BacktestKind.RuleEngine, whatIfJson: null,
                db, accounts, runner, candles, options.Value, user, ct))
        .WithName("QueueBacktestRun")
        .WithSummary("Queue a rule-engine backtest")
        .WithDescription("Validates the range, checks candle coverage for both the run interval and the one-minute drill-down, and refuses overlapping ranges on a sequential account. Returns 202 with a run id; the worker executes it. A run over gapped data is refused unless allowGaps is set, because a backtest across a hole in the data silently lies.")
        .Produces<BacktestRunResponse>(StatusCodes.Status202Accepted)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status501NotImplemented);

        group.MapGet("/{id:guid}", async (
            Guid id,
            TradeLedgerDbContext db,
            CancellationToken ct) =>
        {
            var run = await db.BacktestRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct)
                ?? throw new ResourceNotFoundException("Backtest run", id);

            return Results.Ok(BacktestRunResponse.Of(run, includeRule: true));
        })
        .WithName("GetBacktestRun")
        .WithSummary("Run status and result")
        .WithDescription("Progress while running; once finished, the performance summary plus engine counters such as how many exits were resolved by minute data versus assumed. A run whose exits were mostly assumed is a weak result and says so here. This fetch also returns the frozen copy of the rule the run executed, so an old result stays explainable after the strategy has moved on; the list omits it.")
        .Produces<BacktestRunResponse>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/cancel", async (
            Guid id,
            TradeLedgerDbContext db,
            CancellationToken ct) =>
        {
            var run = await db.BacktestRuns.FirstOrDefaultAsync(r => r.Id == id, ct)
                ?? throw new ResourceNotFoundException("Backtest run", id);

            run.RequestCancellation();
            await db.SaveChangesAsync(ct);

            return Results.Ok(BacktestRunResponse.Of(run));
        })
        .WithName("CancelBacktestRun")
        .WithSummary("Cancel a queued or running backtest")
        .WithDescription("Partial output is discarded rather than kept, so a sequential account's balance chain stays correct.")
        .Produces<BacktestRunResponse>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}", async (
            Guid id,
            TradeLedgerDbContext db,
            BacktestAccountService accounts,
            CancellationToken ct) =>
        {
            var run = await db.BacktestRuns.FirstOrDefaultAsync(r => r.Id == id, ct);

            if (run is null)
            {
                throw new ResourceNotFoundException("Backtest run", id);
            }

            if (run.Status == BacktestStatus.Succeeded
                && !await accounts.IsMostRecentRunAsync(run.BacktestAccountId, run.Id, ct))
            {
                throw new ResourceConflictException(
                    "run_not_most_recent",
                    "Deleting this run would break the balance chain every later run was built on. Delete the later runs first, or switch the account to independent mode.");
            }

            db.BacktestRuns.Remove(run);
            await db.SaveChangesAsync(ct);

            return Results.NoContent();
        })
        .WithName("DeleteBacktestRun")
        .WithSummary("Delete a run and its output")
        .WithDescription("On a sequential account only the most recent succeeded run can be removed: every later run opened at this one's closing balance, so deleting it from the middle would silently invalidate them.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/{id:guid}/trades", async (
            Guid id,
            TradeLedgerDbContext db,
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            CancellationToken ct) =>
        {
            if (!await db.BacktestRuns.AnyAsync(r => r.Id == id, ct))
            {
                throw new ResourceNotFoundException("Backtest run", id);
            }

            var take = Math.Clamp(pageSize ?? 100, 1, 500);
            var skip = Math.Max(page ?? 1, 1) - 1;

            var query = db.BacktestTrades
                .AsNoTracking()
                .Where(t => t.BacktestRunId == id);

            var total = await query.CountAsync(ct);

            var trades = await query
                .OrderBy(t => t.Sequence)
                .Skip(skip * take)
                .Take(take)
                .ToListAsync(ct);

            return Results.Ok(new PagedResult<BacktestTradeResponse>(
                [.. trades.Select(BacktestTradeResponse.From)],
                skip + 1,
                take,
                total));
        })
        .WithName("GetBacktestRunTrades")
        .WithSummary("Simulated trades from a run")
        .WithDescription("Each trade records how its exit was decided: unambiguous, resolved by drilling into one-minute candles, or assumed pessimistically because the bar held both the stop and the target and finer data could not separate them. Paged the same way as the runs list, total included, so a page count can be derived rather than guessed at.")
        .Produces<PagedResult<BacktestTradeResponse>>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/trades/{tradeId:guid}", async (
            Guid id,
            Guid tradeId,
            TradeLedgerDbContext db,
            CancellationToken ct) =>
        {
            var trade = await db.BacktestTrades
                .AsNoTracking()
                .Include(t => t.Executions)
                .FirstOrDefaultAsync(t => t.Id == tradeId && t.BacktestRunId == id, ct)
                ?? throw new ResourceNotFoundException("Backtest trade", tradeId);

            return Results.Ok(BacktestTradeDetailResponse.From(trade));
        })
        .WithName("GetBacktestRunTrade")
        .WithSummary("One simulated position in full")
        .WithDescription("Everything the engine recorded about a single position: when it was opened and closed and for how many bars it was held, entry and exit price, the stop, target and liquidation levels it was sized against, gross and net PnL with fees and funding broken out, achieved versus planned R, MAE and MFE, and the balance it left behind. The fills are folded in rather than fetched separately, so one call explains one position end to end.")
        .Produces<BacktestTradeDetailResponse>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/trades/{tradeId:guid}/executions", async (
            Guid id,
            Guid tradeId,
            TradeLedgerDbContext db,
            CancellationToken ct) =>
        {
            if (!await db.BacktestTrades.AnyAsync(t => t.Id == tradeId && t.BacktestRunId == id, ct))
            {
                throw new ResourceNotFoundException("Backtest trade", tradeId);
            }

            var executions = await db.BacktestExecutions
                .AsNoTracking()
                .Where(e => e.BacktestTradeId == tradeId)
                .OrderBy(e => e.ExecutedAt)
                .ThenBy(e => e.BarIndex)
                .ToListAsync(ct);

            return Results.Ok(executions.Select(BacktestExecutionResponse.From).ToList());
        })
        .WithName("GetBacktestTradeExecutions")
        .WithSummary("Fills behind one simulated trade")
        .WithDescription("The individual fills the engine recorded for a simulated trade: an Open plus a Close or Liquidation, each with its price, quantity, fee, timestamp and the index of the bar it happened on. The engine holds one position at a time with a single entry fill, so today this is always two rows; it is the same shape as the real journal's executions and grows with the engine.")
        .Produces<List<BacktestExecutionResponse>>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/equity-curve", async (
            Guid id,
            TradeLedgerDbContext db,
            CancellationToken ct) =>
        {
            if (!await db.BacktestRuns.AnyAsync(r => r.Id == id, ct))
            {
                throw new ResourceNotFoundException("Backtest run", id);
            }

            var points = await db.BacktestEquityPoints
                .AsNoTracking()
                .Where(p => p.BacktestRunId == id)
                .OrderBy(p => p.Sequence)
                .Select(p => new EquityPoint(p.At, p.Equity))
                .ToListAsync(ct);

            return Results.Ok(EquityMath.Analyse(points));
        })
        .WithName("GetBacktestRunEquityCurve")
        .WithSummary("Equity curve for one run")
        .WithDescription("One point per closed trade. Maximum drawdown here is measured between closed trades; the run's summary also carries maxIntrabarDrawdown, which marks each bar's adverse extreme against an open position and is always the larger of the two.")
        .Produces<EquityCurve>()
        .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> QueueAsync(
        QueueBacktestRequest request,
        BacktestKind kind,
        string? whatIfJson,
        TradeLedgerDbContext db,
        BacktestAccountService accounts,
        BacktestRunner runner,
        CandleRepository candles,
        BacktestOptions options,
        IUserContext user,
        CancellationToken ct)
    {
        if (!runner.SupportsKind(kind))
        {
            throw new EngineUnavailableException(kind.ToString());
        }

        var account = await db.BacktestAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == request.BacktestAccountId, ct);

        if (account is null)
        {
            throw new ResourceNotFoundException("Backtest account", request.BacktestAccountId);
        }

        account.EnsureAcceptsNewRuns();

        if (request.From >= request.To)
        {
            throw new DomainValidationException("from", "from must be earlier than to.");
        }

        var interval = request.Interval ?? CandleInterval.OneHour;

        if (!interval.IsTradeable())
        {
            throw new DomainValidationException(
                "interval",
                "One-minute candles are not a tradeable interval. They exist only to resolve which of a stop or target was hit first inside a larger bar. Pick 15 minutes or longer.");
        }

        var risk = request.RiskPercentPerPosition ?? options.RiskPercentPerPosition;

        if (risk < BacktestOptions.MinRiskPercent || risk > BacktestOptions.MaxRiskPercent)
        {
            throw new DomainValidationException(
                "riskPercentPerPosition",
                $"Risk per position must be between {BacktestOptions.MinRiskPercent} and {BacktestOptions.MaxRiskPercent} percent.");
        }

        var ratio = request.RiskRewardRatio ?? options.RiskRewardRatio;

        if (ratio < BacktestOptions.MinRiskRewardRatio)
        {
            throw new DomainValidationException(
                "riskRewardRatio",
                $"Every position must offer at least {BacktestOptions.MinRiskRewardRatio} to 1.");
        }

        var leverage = request.Leverage ?? options.DefaultLeverage;

        if (leverage < 1 || leverage > options.MaxLeverage)
        {
            throw new DomainValidationException(
                "leverage",
                $"Leverage must be between 1 and {options.MaxLeverage}.");
        }

        if (await accounts.OverlapsExistingRunAsync(account.Id, request.From, request.To, null, ct))
        {
            throw new ResourceConflictException(
                "run_range_overlaps",
                "This range overlaps an existing run on the account. A sequential account chains runs so balances compound; overlapping ranges would count the same market twice. Pick a range that starts after the last run ends, or use an independent account.");
        }

        var source = request.Source ?? CandleSource.BinanceFutures;
        var symbol = (request.Symbol ?? string.Empty).Trim().ToUpperInvariant();

        // The gaps are looked for whether or not they are permitted: allowGaps decides
        // whether a hole refuses the run, and the same answer decides what DataQuality
        // the run is stamped with. Deriving the stamp from the flag instead would mean
        // a run over complete data reads as gapped for the rest of its life.
        var foundGaps = false;

        if (kind == BacktestKind.RuleEngine)
        {
            if (string.IsNullOrWhiteSpace(symbol))
            {
                throw new DomainValidationException(
                    "symbol",
                    "A rule engine run needs the symbol it should trade.");
            }

            var gaps = await candles.FindGapsAsync(
                source, symbol, interval, request.From, request.To, ct);

            if (gaps.Count > 0)
            {
                if (request.AllowGaps != true)
                {
                    throw new DomainRuleException("candle_data_has_gaps", BuildGapDetail(gaps, interval));
                }

                foundGaps = true;
            }
        }

        var run = BacktestRun.Queue(new NewBacktestRun
        {
            UserId = user.UserId ?? Guid.Empty,
            BacktestAccountId = account.Id,
            Kind = kind,
            BacktestStrategyId = request.BacktestStrategyId,
            Symbol = symbol,
            Source = source,
            Interval = interval,
            From = request.From,
            To = request.To,
            RiskPercentPerPosition = risk,
            RiskRewardRatio = ratio,
            Leverage = leverage,
            TakerFeeRate = request.TakerFeeRate ?? options.DefaultTakerFeeRate,
            MakerFeeRate = request.MakerFeeRate ?? options.DefaultMakerFeeRate,
            SlippageRate = request.SlippageRate ?? options.DefaultSlippageRate,
            MaintenanceMarginRate =
                request.MaintenanceMarginRate ?? options.DefaultMaintenanceMarginRate,
            IncludeFunding = request.IncludeFunding ?? true,
            AllowGaps = request.AllowGaps ?? false,
            EngineVersion = options.EngineVersion,
            WhatIfJson = whatIfJson,
        });

        if (foundGaps)
        {
            run.MarkDataQuality(DataQuality.Gapped);
        }

        if (request.BacktestStrategyId is { } strategyId)
        {
            var strategy = await db.BacktestStrategies
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == strategyId, ct);

            if (strategy is null)
            {
                throw new ResourceNotFoundException("Backtest strategy", strategyId);
            }

            run.UseRules(strategy.RuleJson, strategy.RuleHash);
        }

        db.BacktestRuns.Add(run);
        await db.SaveChangesAsync(ct);

        return Results.Accepted($"/api/backtests/{run.Id}", BacktestRunResponse.Of(run));
    }

    private static string BuildGapDetail(
        IReadOnlyList<CandleGap> gaps,
        CandleInterval interval)
    {
        var missing = gaps.Sum(g => (long)g.MissingCount);
        var shown = gaps.Take(5).Select(g => $"{g.From:u} to {g.To:u} ({g.MissingCount} bars)");

        var detail =
            $"{missing} {interval} candle(s) are missing across {gaps.Count} gap(s): " +
            string.Join("; ", shown);

        if (gaps.Count > 5)
        {
            detail += $"; and {gaps.Count - 5} more";
        }

        return detail +
            ". Backfill the range first, or set allowGaps to run anyway and have the result stamped as gapped.";
    }

    internal static BacktestResultSummary? DeserializeSummary(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? null
            : JsonSerializer.Deserialize<BacktestResultSummary>(json, Json);

    internal static IReadOnlyList<string> DeserializeWarnings(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? []
            : JsonSerializer.Deserialize<List<string>>(json, Json) ?? [];
}
