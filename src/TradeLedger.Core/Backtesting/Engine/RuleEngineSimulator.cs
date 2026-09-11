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
        var warmupBars = document.WarmupBars;

        var bars = await candles
            .GetRangeAsync(
                source,
                run.Symbol,
                interval,
                run.From - interval.Duration() * warmupBars,
                run.To,
                ct)
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

        if (startIndex < warmupBars)
        {
            warnings.Add(
                $"Only {startIndex} bars of history were available before the start date but the " +
                $"indicators need {warmupBars}, so the run begins once they are warm.");

            startIndex = warmupBars;
        }

        if (startIndex >= bars.Count)
        {
            throw new InvalidOperationException(
                $"The indicators need {warmupBars} bars of warmup and this range does not hold enough " +
                "data. Backfill earlier candles, or start the run later.");
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
