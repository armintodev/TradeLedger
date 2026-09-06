using TradeLedger.Core.Domain;

namespace TradeLedger.Api.Features.Trades;

public sealed record TradeDetailResponse(
    Guid Id,
    Guid AccountId,
    string Symbol,
    TradeSide Side,
    TradeOrigin Origin,
    ReviewState ReviewState,
    TradeOutcome Outcome,
    bool IsPlanned,
    string? ExchangePositionId,
    DateTimeOffset OpenedAt,
    DateTimeOffset? ClosedAt,
    decimal EntryPrice,
    decimal? ExitPrice,
    decimal Quantity,
    decimal? PositionMargin,
    int Leverage,
    decimal? OrderValue,
    MarginMode? MarginMode,
    PositionMode? PositionMode,
    OrderType OrderType,
    decimal? PercentClosed,
    decimal? LiquidatedQuantity,
    decimal? LiquidationPrice,
    decimal? StopLossPrice,
    decimal? TakeProfitPrice,
    decimal? PositionToAccountPercent,
    decimal? PlannedStopLossPercent,
    decimal? AccountRiskedPercent,
    decimal? PlannedReturnR,
    decimal GrossProfitLoss,
    decimal Fees,
    decimal Funding,
    decimal NetProfitLoss,
    decimal? AchievedReturnR,
    decimal? TradeGainPercent,
    decimal? AccountChangePercent,
    decimal? BalanceAfter,
    TimeSpan? Duration,
    string? StrategyName,
    string? TimeframeName,
    string? EntryTypeName,
    string? ExitTypeName,
    string? EntryMentalStateName,
    string? ExitMentalStateName,
    MarketContext? MarketContext,
    int? Rating,
    string? Memo,
    string? Tag,
    string? PostTradeTag,
    Guid? TradePlanId,
    List<ExecutionResponse> Executions,
    List<TradeTermResponse> Mistakes,
    List<TradeTermResponse> Trackings,
    List<AttachmentResponse> Attachments)
{
    public static TradeDetailResponse From(Trade t) => new(
        t.Id, t.AccountId, t.Symbol, t.Side, t.Origin, t.ReviewState, t.Outcome, t.IsPlanned,
        t.ExchangePositionId, t.OpenedAt, t.ClosedAt, t.EntryPrice, t.ExitPrice, t.Quantity,
        t.PositionMargin, t.Leverage, t.OrderValue, t.MarginMode, t.PositionMode, t.OrderType,
        t.PercentClosed, t.LiquidatedQuantity, t.LiquidationPrice,
        t.StopLossPrice, t.TakeProfitPrice, t.PositionToAccountPercent, t.PlannedStopLossPercent,
        t.AccountRiskedPercent, t.PlannedReturnR,
        t.GrossProfitLoss, t.Fees, t.Funding, t.NetProfitLoss, t.AchievedReturnR,
        t.TradeGainPercent, t.AccountChangePercent, t.BalanceAfter, t.Duration,
        t.Strategy?.Name, t.Timeframe?.Name, t.EntryType?.Name, t.ExitType?.Name,
        t.EntryMentalState?.Name, t.ExitMentalState?.Name,
        t.MarketContext, t.Rating, t.Memo, t.Tag, t.PostTradeTag, t.TradePlanId,
        [.. t.Executions.OrderBy(e => e.ExecutedAt).Select(ExecutionResponse.From)],
        [.. t.Mistakes.Select(m => new TradeTermResponse(m.TaxonomyTermId, m.Term?.Name ?? string.Empty, m.Note, m.EstimatedCost))],
        [.. t.Trackings.Select(x => new TradeTermResponse(x.TaxonomyTermId, x.Term?.Name ?? string.Empty, x.Note, null))],
        [.. t.Attachments.Select(AttachmentResponse.From)]);
}

public sealed record ExecutionResponse(
    Guid Id,
    ExecutionRole Role,
    TradeSide Side,
    decimal Price,
    decimal Quantity,
    decimal Fee,
    string FeeAsset,
    decimal? RealizedProfitLoss,
    DateTimeOffset ExecutedAt,
    string? ExchangeTradeId,
    string? ExchangeOrderId,
    OrderType OrderType)
{
    public static ExecutionResponse From(Execution e) => new(
        e.Id, e.Role, e.Side, e.Price, e.Quantity, e.Fee, e.FeeAsset,
        e.RealizedProfitLoss, e.ExecutedAt, e.ExchangeTradeId, e.ExchangeOrderId, e.OrderType);
}

public sealed record TradeTermResponse(Guid TermId, string Name, string? Note, decimal? EstimatedCost);

public sealed record AttachmentResponse(
    Guid Id,
    string Slot,
    string FileName,
    string ContentType,
    long SizeBytes,
    string? Caption,
    DateTimeOffset UploadedAt)
{
    public static AttachmentResponse From(Attachment a) => new(
        a.Id, a.Slot, a.FileName, a.ContentType, a.SizeBytes, a.Caption, a.UploadedAt);
}
