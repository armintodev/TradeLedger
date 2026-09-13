using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TradeLedger.Core.Domain.MarketData;
using TradeLedger.Core.MarketData;
using TradeLedger.Core.Persistence;
using TradeLedger.Core.Shared;

namespace TradeLedger.Api.Features.MarketData;

public sealed record MarketDataCoverageResponse(
    CandleSource Source,
    string Symbol,
    CandleInterval Interval,
    long RowCount,
    DateTimeOffset? FirstOpenTime,
    DateTimeOffset? LastOpenTime,
    bool IsTradeable)
{
    public static MarketDataCoverageResponse From(CandleCoverage c) => new(
        c.Source, c.Symbol, c.Interval, c.RowCount,
        c.FirstOpenTime, c.LastOpenTime, c.Interval.IsTradeable());
}

public sealed record CandleGapResponse(
    DateTimeOffset From,
    DateTimeOffset To,
    int MissingCount)
{
    public static CandleGapResponse Of(CandleGap g) => new(g.From, g.To, g.MissingCount);
}

public sealed record BackfillJobResponse(
    Guid Id,
    CandleSource Source,
    string Symbol,
    CandleInterval Interval,
    DateTimeOffset From,
    DateTimeOffset To,
    MarketDataJobStatus Status,
    int CandlesWritten,
    int FundingRatesWritten,
    decimal ProgressPercent,
    bool CancellationRequested,
    bool IncludeFundingRates,
    DateTimeOffset QueuedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    string? Error)
{
    public static BackfillJobResponse Of(MarketDataBackfillJob j) => new(
        j.Id, j.Source, j.Symbol, j.Interval, j.From, j.To, j.Status,
        j.CandlesWritten, j.FundingRatesWritten, j.ProgressPercent,
        j.CancellationRequested, j.IncludeFundingRates,
        j.QueuedAt, j.StartedAt, j.FinishedAt, j.Error);
}

public sealed record CandleImportResponse(
    Guid Id,
    string FileName,
    CandleSource Source,
    string Symbol,
    CandleInterval Interval,
    int RowsParsed,
    int RowsInserted,
    int RowsSkippedAsDuplicate,
    DateTimeOffset? FirstOpenTime,
    DateTimeOffset? LastOpenTime,
    IReadOnlyList<string> Warnings)
{
    public static CandleImportResponse From(CandleImport i, IReadOnlyList<string> warnings) => new(
        i.Id, i.FileName, i.Source, i.Symbol, i.Interval,
        i.RowsParsed, i.RowsInserted, i.RowsSkippedAsDuplicate,
        i.FirstOpenTime, i.LastOpenTime, warnings);
}

public sealed record CandleBarResponse(
    DateTimeOffset OpenTime,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    decimal Volume)
{
    public static CandleBarResponse Of(CandleBar b) => new(
        b.OpenTime, b.Open, b.High, b.Low, b.Close, b.Volume);
}

/// <param name="Total">Stored candles in the range, before any aggregation.</param>
/// <param name="Returned">Bars in this response.</param>
/// <param name="BucketSize">Stored candles behind each returned bar; 1 when untouched.</param>
public sealed record CandleSeriesResponse(
    CandleSource Source,
    string Symbol,
    CandleInterval Interval,
    DateTimeOffset From,
    DateTimeOffset To,
    int Total,
    int Returned,
    int BucketSize,
    bool IsDownsampled,
    IReadOnlyList<CandleBarResponse> Candles)
{
    public static CandleSeriesResponse Of(CandleQuery q, CandleSeries s) => new(
        q.Source,
        q.Symbol.Trim().ToUpperInvariant(),
        q.Interval,
        q.From,
        q.To,
        s.Total,
        s.Bars.Count,
        s.BucketSize,
        s.IsDownsampled,
        [.. s.Bars.Select(CandleBarResponse.Of)]);
}

public sealed record DeleteCandlesResponse(int Deleted);
