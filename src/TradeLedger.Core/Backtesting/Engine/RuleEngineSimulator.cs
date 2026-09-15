using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TradeLedger.Core.Backtesting.Rules;
using TradeLedger.Core.Domain.Backtesting;
using TradeLedger.Core.Domain.MarketData;
using TradeLedger.Core.MarketData;
using TradeLedger.Core.Persistence;

namespace TradeLedger.Core.Backtesting.Engine;

public sealed class RuleEngineSimulator(
    TradeLedgerDbContext db,
    CandleRepository candles,
    ILogger<RuleEngineSimulator> logger) : IBacktestEngine
{
    public BacktestKind Kind => BacktestKind.RuleEngine;

    public async Task<BacktestEngineResult> RunAsync(
        BacktestRun run,
        IBacktestProgress progress,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(run.RuleJson))
        {
            throw new InvalidOperationException(
                "This run has no strategy rules attached, so there is nothing to simulate.");
        }

        if (run.Symbol is null || run.Source is not { } source || run.Interval is not { } interval)
        {
            throw new InvalidOperationException(
                "A rule-engine run needs a symbol, a candle source and an interval.");
        }

        var document = RuleDocumentParser.Parse(run.RuleJson);

        // Queue time rejects this already; re-checked because a queued run executes later,
        // in another process, against a document it only holds by value.
        foreach (var declared in document.IntervalsFor(interval))
        {
            if (!interval.DividesInto(declared))
            {
                throw new InvalidOperationException(
                    $"An indicator reads {declared} candles, which cannot be built from this run's " +
                    $"{interval} bars. An indicator interval must be at or above the run's, and a " +
                    "whole multiple of it.");
            }
        }

        // An instant rather than a bar count: each indicator's warmup is floored onto its own
        // grid, and the earliest result is the one that satisfies all of them. See SPEC 6.8.4.
        var loadFrom = document.LoadFrom(interval, run.From);

        var bars = await candles
            .GetRangeAsync(source, run.Symbol, interval, loadFrom, run.To, ct)
            .ConfigureAwait(false);

        if (bars.Count == 0)
        {
            throw new InvalidOperationException(
                $"No {interval} candles are stored for {run.Symbol} in this range. Backfill it first.");
        }

        var warnings = new List<string>();
        var startIndex = bars.FindIndex(b => b.OpenTime >= run.From);

        if (startIndex < 0)
        {
            throw new InvalidOperationException("No candles fall inside the requested range.");
        }

        var funding = run.IncludeFunding
            ? await db.FundingRates
                .AsNoTracking()
                .Where(r => r.Source == source
                            && r.Symbol == run.Symbol
                            && r.FundingTime >= run.From
                            && r.FundingTime <= run.To)
                .OrderBy(r => r.FundingTime)
                .ToListAsync(ct)
                .ConfigureAwait(false)
            : [];

        if (run.IncludeFunding && funding.Count == 0)
        {
            warnings.Add(
                "No funding rates are stored for this symbol and range, so funding was treated as zero.");
        }

        var parameters = new SimulationParameters(
            run.Symbol,
            run.From,
            run.OpeningBalance,
            run.RiskPercentPerPosition,
            run.RiskRewardRatio,
            run.Leverage,
            run.MaintenanceMarginRate,
            new CostModel(run.TakerFeeRate, run.MakerFeeRate, run.SlippageRate));

        var result = await RuleSimulationCore
            .RunAsync(
                document,
                bars,
                startIndex,
                parameters,
                funding,
                new MinuteCandleCache(candles, source, run.Symbol),
                progress,
                warnings,
                ct)
            .ConfigureAwait(false);

        logger.LogInformation(
            "Rule engine produced {Trades} trades over {Bars} bars for {Symbol}.",
            result.Trades.Count, bars.Count - startIndex, run.Symbol);

        return result;
    }

    private sealed class MinuteCandleCache(
        CandleRepository candles,
        CandleSource source,
        string symbol) : IMinuteCandleSource
    {
        private readonly Dictionary<DateOnly, List<Candle>> _days = [];

        public async Task<IReadOnlyList<Candle>> GetAsync(
            DateTimeOffset from,
            DateTimeOffset to,
            CancellationToken ct)
        {
            var results = new List<Candle>();

            for (var day = DateOnly.FromDateTime(from.UtcDateTime);
                 day <= DateOnly.FromDateTime(to.UtcDateTime);
                 day = day.AddDays(1))
            {
                if (!_days.TryGetValue(day, out var loaded))
                {
                    var start = new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

                    loaded = await candles
                        .GetRangeAsync(
                            source,
                            symbol,
                            CandleInterval.OneMinute,
                            start,
                            start.AddDays(1).AddTicks(-1),
                            ct)
                        .ConfigureAwait(false);

                    _days[day] = loaded;
                }

                results.AddRange(loaded.Where(c => c.OpenTime >= from && c.OpenTime < to));
            }

            return results;
        }
    }
}
