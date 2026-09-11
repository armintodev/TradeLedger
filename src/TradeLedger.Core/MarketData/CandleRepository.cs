using Microsoft.EntityFrameworkCore;
using TradeLedger.Core.Domain.MarketData;
using TradeLedger.Core.Persistence;

namespace TradeLedger.Core.MarketData;

public sealed class CandleRepository(TradeLedgerDbContext db)
{
    public async Task<int> UpsertAsync(
        IReadOnlyList<Candle> candles,
        CancellationToken ct = default)
    {
        if (candles.Count == 0)
        {
            return 0;
        }

        var source = candles[0].Source;
        var symbol = candles[0].Symbol;
        var interval = candles[0].Interval;

        var first = candles.Min(c => c.OpenTime);
        var last = candles.Max(c => c.OpenTime);

        var existing = await db.Candles
            .AsNoTracking()
            .Where(c => c.Source == source
                        && c.Symbol == symbol
                        && c.Interval == interval
                        && c.OpenTime >= first
                        && c.OpenTime <= last)
            .Select(c => c.OpenTimeRawMs)
            .ToListAsync(ct);

        var known = existing.ToHashSet();

        var fresh = candles
            .Where(c => known.Add(c.OpenTimeRawMs))
            .ToList();

        if (fresh.Count == 0)
        {
            return 0;
        }

        db.Candles.AddRange(fresh);
        await db.SaveChangesAsync(ct);

        foreach (var candle in fresh)
        {
            db.Entry(candle).State = EntityState.Detached;
        }

        return fresh.Count;
    }

    public async Task<int> UpsertFundingRatesAsync(
        IReadOnlyList<FundingRateHistory> rates,
        CancellationToken ct = default)
    {
        if (rates.Count == 0)
        {
            return 0;
        }

        var source = rates[0].Source;
        var symbol = rates[0].Symbol;

        var first = rates.Min(r => r.FundingTime);
        var last = rates.Max(r => r.FundingTime);

        var existing = await db.FundingRates
            .AsNoTracking()
            .Where(r => r.Source == source
                        && r.Symbol == symbol
                        && r.FundingTime >= first
                        && r.FundingTime <= last)
            .Select(r => r.FundingTimeRawMs)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var known = existing.ToHashSet();
        var fresh = rates.Where(r => known.Add(r.FundingTimeRawMs)).ToList();

        if (fresh.Count == 0)
        {
            return 0;
        }

        db.FundingRates.AddRange(fresh);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        foreach (var rate in fresh)
        {
            db.Entry(rate).State = EntityState.Detached;
        }

        return fresh.Count;
    }

    public Task<List<Candle>> GetRangeAsync(
        CandleSource source,
        string symbol,
        CandleInterval interval,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default) =>
        db.Candles
            .AsNoTracking()
            .Where(c => c.Source == source
                        && c.Symbol == symbol
                        && c.Interval == interval
                        && c.OpenTime >= from
                        && c.OpenTime <= to)
            .OrderBy(c => c.OpenTime)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<CandleGap>> FindGapsAsync(
        CandleSource source,
        string symbol,
        CandleInterval interval,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default)
    {
        var duration = interval.Duration();
        var start = interval.AlignFloor(from);
        var end = interval.AlignFloor(to);

        if (end < start)
        {
            return [];
        }

        var expectedCount = (long)((end - start).Ticks / duration.Ticks) + 1;

        var summary = await db.Candles
            .AsNoTracking()
            .Where(c => c.Source == source
                        && c.Symbol == symbol
                        && c.Interval == interval
                        && c.OpenTime >= start
                        && c.OpenTime <= end)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Count = g.LongCount(),
                Min = g.Min(c => c.OpenTime),
                Max = g.Max(c => c.OpenTime),
            })
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (summary is null)
        {
            return [new CandleGap(start, end, (int)Math.Min(expectedCount, int.MaxValue))];
        }

        if (summary.Count == expectedCount && summary.Min == start && summary.Max == end)
        {
            return [];
        }

        var present = await db.Candles
            .AsNoTracking()
            .Where(c => c.Source == source
                        && c.Symbol == symbol
                        && c.Interval == interval
                        && c.OpenTime >= start
                        && c.OpenTime <= end)
            .Select(c => c.OpenTime)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var known = present.ToHashSet();
        var gaps = new List<CandleGap>();

        DateTimeOffset? runStart = null;
        DateTimeOffset? runEnd = null;
        var missing = 0;

        for (var at = start; at <= end; at += duration)
        {
            if (known.Contains(at))
            {
                if (runStart is not null)
                {
                    gaps.Add(new CandleGap(runStart.Value, runEnd!.Value, missing));
                    runStart = null;
                    runEnd = null;
                    missing = 0;
                }

                continue;
            }

            runStart ??= at;
            runEnd = at;
            missing++;
        }

        if (runStart is not null)
        {
            gaps.Add(new CandleGap(runStart.Value, runEnd!.Value, missing));
        }

        return gaps;
    }

    public async Task<IReadOnlyList<CandleCoverage>> GetCoverageAsync(
        CancellationToken ct = default)
    {
        var rows = await db.Candles
            .AsNoTracking()
            .GroupBy(c => new { c.Source, c.Symbol, c.Interval })
            .Select(g => new CandleCoverage(
                g.Key.Source,
                g.Key.Symbol,
                g.Key.Interval,
                g.LongCount(),
                g.Min(c => c.OpenTime),
                g.Max(c => c.OpenTime)))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return [.. rows.OrderBy(r => r.Symbol).ThenBy(r => r.Interval)];
    }

    public Task<int> DeleteRangeAsync(
        CandleSource source,
        string symbol,
        CandleInterval interval,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default) =>
        db.Candles
            .Where(c => c.Source == source
                        && c.Symbol == symbol
                        && c.Interval == interval
                        && c.OpenTime >= from
                        && c.OpenTime <= to)
            .ExecuteDeleteAsync(ct);

    public async Task<string> ResolveSymbolAsync(
        string canonicalSymbol,
        CandleSource source,
        CancellationToken ct = default)
    {
        var alias = await db.MarketSymbolAliases
            .AsNoTracking()
            .Where(a => a.CanonicalSymbol == canonicalSymbol && a.Source == source)
            .Select(a => a.SourceSymbol)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        return alias ?? canonicalSymbol;
    }
}
