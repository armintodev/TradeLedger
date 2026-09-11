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
                [.. runs.Select(BacktestRunResponse.Of)],
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
            var run = await db.BacktestRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);

            return run is null ? Results.NotFound() : Results.Ok(BacktestRunResponse.Of(run));
        })
        .WithName("GetBacktestRun")
        .WithSummary("Run status and result")
        .WithDescription("Progress while running; once finished, the performance summary plus engine counters such as how many exits were resolved by minute data versus assumed. A run whose exits were mostly assumed is a weak result and says so here.")
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

            var trades = await db.BacktestTrades
                .AsNoTracking()
                .Where(t => t.BacktestRunId == id)
                .OrderBy(t => t.Sequence)
                .Skip(skip * take)
                .Take(take)
                .ToListAsync(ct);

            return Results.Ok(trades.Select(BacktestTradeResponse.From).ToList());
        })
        .WithName("GetBacktestRunTrades")
        .WithSummary("Simulated trades from a run")
        .WithDescription("Each trade records how its exit was decided: unambiguous, resolved by drilling into one-minute candles, or assumed pessimistically because the bar held both the stop and the target and finer data could not separate them.")
        .Produces<List<BacktestTradeResponse>>()
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

        if (kind == BacktestKind.RuleEngine)
        {
            if (string.IsNullOrWhiteSpace(symbol))
            {
                throw new DomainValidationException(
                    "symbol",
                    "A rule engine run needs the symbol it should trade.");
            }

            if (request.AllowGaps != true)
            {
                var gaps = await candles.FindGapsAsync(
                    source, symbol, interval, request.From, request.To, ct);

                if (gaps.Count > 0)
                {
                    throw new DomainRuleException("candle_data_has_gaps", BuildGapDetail(gaps, interval));
                }
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
