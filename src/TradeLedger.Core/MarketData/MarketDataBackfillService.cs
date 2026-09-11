using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TradeLedger.Core.Domain.MarketData;
using TradeLedger.Core.Persistence;
using TradeLedger.Core.Shared.Proxy;

namespace TradeLedger.Core.MarketData;

public sealed class MarketDataBackfillService(
    TradeLedgerDbContext db,
    CandleRepository candles,
    IKlineSource klines,
    IUserProxyResolver proxyResolver,
    ILogger<MarketDataBackfillService> logger)
{
    private const int ChunkDays = 30;

    public async Task RunAsync(Guid jobId, CancellationToken ct = default)
    {
        db.BypassUserFilter = true;

        var job = await db.Set<MarketDataBackfillJob>()
            .FirstOrDefaultAsync(j => j.Id == jobId, ct)
            .ConfigureAwait(false);

        if (job is null || job.Status != MarketDataJobStatus.Queued)
        {
            return;
        }

        job.Start();
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        try
        {
            var proxy = await proxyResolver.ResolveAsync(job.UserId, ct).ConfigureAwait(false);

            logger.LogInformation(
                "Backfilling {Interval} {Symbol} from {Source} between {From} and {To} via {Egress}.",
                job.Interval, job.Symbol, job.Source, job.From, job.To,
                proxy?.Describe() ?? "direct");

            var sourceSymbol = await candles
                .ResolveSymbolAsync(job.Symbol, job.Source, ct)
                .ConfigureAwait(false);

            var written = 0;
            var chunk = TimeSpan.FromDays(ChunkDays);
            var total = (job.To - job.From).Ticks;
            var cursor = job.From;

            while (cursor < job.To)
            {
                ct.ThrowIfCancellationRequested();

                if (await IsCancelledAsync(job.Id, ct).ConfigureAwait(false))
                {
                    job.Cancel();
                    await db.SaveChangesAsync(ct).ConfigureAwait(false);
                    return;
                }

                var chunkEnd = cursor + chunk > job.To ? job.To : cursor + chunk;

                var batch = await klines
                    .FetchKlinesAsync(job.Source, sourceSymbol, job.Interval, cursor, chunkEnd, proxy, ct)
                    .ConfigureAwait(false);

                foreach (var candle in batch)
                {
                    candle.AttributeTo(job.Symbol);
                }

                written += await candles.UpsertAsync(batch, ct).ConfigureAwait(false);

                cursor = chunkEnd;

                job.ReportProgress(written, cursor);

                await db.SaveChangesAsync(ct).ConfigureAwait(false);
            }

            if (job is { IncludeFundingRates: true, Source: CandleSource.BinanceFutures })
            {
                var rates = await klines
                    .FetchFundingRatesAsync(sourceSymbol, job.From, job.To, proxy, ct)
                    .ConfigureAwait(false);

                foreach (var rate in rates)
                {
                    rate.AttributeTo(job.Symbol);
                }

                job.RecordFundingRates(
                    await candles.UpsertFundingRatesAsync(rates, ct).ConfigureAwait(false));
            }

            job.Succeed();

            await db.SaveChangesAsync(ct);

            logger.LogInformation(
                "Backfill {JobId} wrote {Candles} candles and {Rates} funding rates.",
                job.Id, job.CandlesWritten, job.FundingRatesWritten);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            job.Cancel();
            await db.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        catch (ProxyRequiredException ex)
        {
            await FailAsync(job, ex.Message).ConfigureAwait(false);
        }
        catch (MarketDataException ex)
        {
            await FailAsync(job, ex.Message).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            await FailAsync(job, $"Market data request failed: {ex.Message}").ConfigureAwait(false);
        }
    }

    private async Task FailAsync(MarketDataBackfillJob job, string error)
    {
        logger.LogError("Backfill {JobId} failed: {Error}", job.Id, error);

        job.Fail(error);

        await db.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
    }

    private Task<bool> IsCancelledAsync(Guid jobId, CancellationToken ct) =>
        db.Set<MarketDataBackfillJob>()
            .AsNoTracking()
            .Where(j => j.Id == jobId)
            .Select(j => j.CancellationRequested)
            .FirstOrDefaultAsync(ct);
}
