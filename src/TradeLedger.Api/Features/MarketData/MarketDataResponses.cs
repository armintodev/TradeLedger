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
    DateTimeOffset QueuedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    string? Error)
{
    public static BackfillJobResponse Of(MarketDataBackfillJob j) => new(
        j.Id, j.Source, j.Symbol, j.Interval, j.From, j.To, j.Status,
        j.CandlesWritten, j.FundingRatesWritten, j.ProgressPercent,
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

public sealed record DeleteCandlesResponse(int Deleted);
