using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TradeLedger.Core.Domain;
using TradeLedger.Core.Domain.MarketData;
using TradeLedger.Core.MarketData;
using TradeLedger.Core.Persistence;
using TradeLedger.Core.Shared;

namespace TradeLedger.Api.Features.MarketData;

public static class MarketDataEndpoints
{
    public static void MapMarketDataEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/market-data")
            .WithTags("Market Data")
            .RequireAuthorization();

        group.MapGet("/coverage", async (
            CandleRepository candles,
            CancellationToken ct) =>
        {
            var rows = await candles.GetCoverageAsync(ct);
            return Results.Ok(rows.Select(MarketDataCoverageResponse.From).ToList());
        })
        .WithName("GetMarketDataCoverage")
        .WithSummary("What candle data is stored")
        .WithDescription("Row count and first and last candle for every source, symbol and interval held locally. Use it to see what a backtest can actually run over, and to watch storage grow. One-minute candles exist only to resolve which of a stop or target was hit first inside a larger bar; they are never a tradeable interval.")
        .Produces<List<MarketDataCoverageResponse>>();

        group.MapGet("/gaps", async (
            CandleRepository candles,
            [AsParameters] GapQuery query,
            CancellationToken ct) =>
        {
            if (query.From >= query.To)
            {
                throw new DomainValidationException("from", "from must be earlier than to.");
            }

            var gaps = await candles.FindGapsAsync(
                query.Source, query.Symbol, query.Interval, query.From, query.To, ct);

            return Results.Ok(gaps.Select(CandleGapResponse.Of).ToList());
        })
        .WithName("GetCandleGaps")
        .WithSummary("Missing candles in a range")
        .WithDescription("Walks every expected interval boundary between from and to and reports the runs that are missing. A backtest over gapped data silently lies, so a run is refused when its range has gaps unless allowGaps is set. Exchange downtime produces real gaps that will never fill.")
        .Produces<List<CandleGapResponse>>()
        .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/backfill", async (
            [FromBody] BackfillRequest request,
            TradeLedgerDbContext db,
            IUserContext user,
            CancellationToken ct) =>
        {
            var job = MarketDataBackfillJob.Queue(
                request.Source,
                request.Symbol,
                request.Interval,
                request.From,
                request.To,
                request.IncludeFundingRates ?? false,
                user.UserId ?? Guid.Empty);

            db.MarketDataBackfillJobs.Add(job);
            await db.SaveChangesAsync(ct);

            return Results.Accepted(
                $"/api/market-data/backfill/{job.Id}", BackfillJobResponse.Of(job));
        })
        .WithName("QueueMarketDataBackfill")
        .WithSummary("Queue a candle backfill")
        .WithDescription("Fetches candles from Binance and stores them. Returns immediately with a job id; the worker does the fetching. Every request leaves through the same per-user proxy as Bitunix, so with Proxy:Required true and no proxy configured the job fails rather than reaching Binance from your own address. Backfill the one-minute interval too if you want intrabar fill resolution.")
        .Produces<BackfillJobResponse>(StatusCodes.Status202Accepted)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/backfill/{id:guid}", async (
            Guid id,
            TradeLedgerDbContext db,
            CancellationToken ct) =>
        {
            var job = await db.MarketDataBackfillJobs
                .AsNoTracking()
                .FirstOrDefaultAsync(j => j.Id == id, ct);

            return job is null
                ? throw new ResourceNotFoundException("Backfill job", id)
                : Results.Ok(BackfillJobResponse.Of(job));
        })
        .WithName("GetMarketDataBackfill")
        .WithSummary("Backfill job status")
        .WithDescription("Progress, candles written and any error for a queued or finished backfill.")
        .Produces<BackfillJobResponse>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/backfill/{id:guid}/cancel", async (
            Guid id,
            TradeLedgerDbContext db,
            CancellationToken ct) =>
        {
            var job = await db.MarketDataBackfillJobs.FirstOrDefaultAsync(j => j.Id == id, ct)
                ?? throw new ResourceNotFoundException("Backfill job", id);

            job.RequestCancellation();
            await db.SaveChangesAsync(ct);

            return Results.Ok(BackfillJobResponse.Of(job));
        })
        .WithName("CancelMarketDataBackfill")
        .WithSummary("Cancel a backfill")
        .WithDescription("Flags a running job to stop at its next chunk boundary. Candles already written are kept, since they are valid on their own.")
        .Produces<BackfillJobResponse>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/import", async (
            IFormFile file,
            [FromForm] CandleSource source,
            [FromForm] string symbol,
            [FromForm] CandleInterval interval,
            CandleRepository candles,
            TradeLedgerDbContext db,
            IUserContext user,
            IOptions<MarketDataOptions> options,
            CancellationToken ct) =>
        {
            if (file.Length == 0)
            {
                throw new DomainValidationException("file", "The uploaded file is empty.");
            }

            if (file.Length > options.Value.MaxImportBytes)
            {
                throw new DomainValidationException(
                    "file",
                    $"The uploaded file is too large; the limit is {options.Value.MaxImportBytes} bytes.");
            }

            var normalisedSymbol = Guard.NotBlank(symbol, "symbol").ToUpperInvariant();

            byte[] bytes;

            await using (var upload = file.OpenReadStream())
            await using (var buffer = new MemoryStream())
            {
                await upload.CopyToAsync(buffer, ct);
                bytes = buffer.ToArray();
            }

            CsvImportResult parsed;

            try
            {
                using var content = new MemoryStream(bytes, writable: false);
                parsed = CsvKlineImporter.Parse(content, source, normalisedSymbol, interval);
            }
            catch (CsvKlineImportException ex)
            {
                throw new DomainValidationException("file", ex.Message);
            }

            var inserted = await candles.UpsertAsync(parsed.Candles, ct);

            var audit = CandleImport.Record(new NewCandleImport
            {
                UserId = user.UserId ?? Guid.Empty,
                FileName = Path.GetFileName(file.FileName),
                FileBytes = file.Length,
                ContentSha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)),
                Source = source,
                Symbol = normalisedSymbol,
                Interval = interval,
                RowsParsed = parsed.RowsParsed,
                RowsInserted = inserted,
                RowsSkippedAsDuplicate = parsed.Candles.Count - inserted + parsed.DuplicateTimestamps,
                FirstOpenTime = parsed.Candles.Count > 0 ? parsed.Candles[0].OpenTime : null,
                LastOpenTime = parsed.Candles.Count > 0 ? parsed.Candles[^1].OpenTime : null,
                Warnings = parsed.Warnings.Count > 0 ? string.Join(" ", parsed.Warnings) : null,
            });

            db.CandleImports.Add(audit);
            await db.SaveChangesAsync(ct);

            return Results.Ok(CandleImportResponse.From(audit, parsed.Warnings));
        })
        .WithName("ImportCandlesFromCsv")
        .WithSummary("Import candles from a CSV file")
        .WithDescription("Accepts a TradingView-style export. Needs a time column plus open, high, low and close; volume is optional and defaults to zero. Timestamps may be ISO-8601 or epoch seconds or milliseconds, and a value with no offset is read as UTC and says so in the response. Rows already present are skipped rather than overwritten, so re-importing the same file is a no-op. The file is rejected outright on non-monotonic or misaligned timestamps, a high below its low, or an open or close outside the bar range.")
        .Produces<CandleImportResponse>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .DisableAntiforgery();

        group.MapDelete("/candles", async (
            CandleRepository candles,
            [AsParameters] GapQuery query,
            CancellationToken ct) =>
        {
            if (query.From >= query.To)
            {
                return Results.Problem(
                    title: "Invalid range",
                    detail: "from must be earlier than to.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var deleted = await candles.DeleteRangeAsync(
                query.Source, query.Symbol, query.Interval, query.From, query.To, ct);

            return Results.Ok(new DeleteCandlesResponse(deleted));
        })
        .WithName("DeleteCandles")
        .WithSummary("Delete stored candles in a range")
        .WithDescription("Removes candles for one source, symbol and interval between from and to. Both bounds are required so a mistyped request cannot empty the table. Deleting data a finished backtest ran over does not change that run's stored results, but it will make the run unreproducible.")
        .Produces<DeleteCandlesResponse>()
        .ProducesProblem(StatusCodes.Status400BadRequest);
    }
}
