using TradeLedger.Core.Domain;

namespace TradeLedger.Api.Features.Trades;

public sealed record MarketContextRequest(
    string? Total2,
    string? BtcDominance,
    string? UsdtDominance,
    string? MarketTrend,
    string? Sma,
    string? MarketSession,
    string? BtcPair,
    string? Rsi,
    string? Volume,
    string? CandleShape)
{
    public MarketContext ToDomain() => MarketContext.Create(
        Total2,
        BtcDominance,
        UsdtDominance,
        MarketTrend,
        Sma,
        MarketSession,
        BtcPair,
        Rsi,
        Volume,
        CandleShape);
}

public sealed record JournalTradeRequest(
    Guid? StrategyId,
    Guid? TimeframeId,
    Guid? EntryTypeId,
    Guid? ExitTypeId,
    Guid? EntryMentalStateId,
    Guid? ExitMentalStateId,
    MarketContextRequest? MarketContext,
    decimal? StopLossPrice,
    decimal? TakeProfitPrice,
    List<Guid>? MistakeIds,
    List<Guid>? TrackingIds,
    int? Rating,
    string? Memo,
    string? Tag,
    string? PostTradeTag,
    bool? MarkReviewed)
{
    public TradeJournalEdit ToEdit() => new()
    {
        StrategyId = StrategyId,
        TimeframeId = TimeframeId,
        EntryTypeId = EntryTypeId,
        ExitTypeId = ExitTypeId,
        EntryMentalStateId = EntryMentalStateId,
        ExitMentalStateId = ExitMentalStateId,
        MarketContext = MarketContext?.ToDomain(),
        StopLossPrice = StopLossPrice,
        TakeProfitPrice = TakeProfitPrice,
        Rating = Rating,
        Memo = Memo,
        Tag = Tag,
        PostTradeTag = PostTradeTag,
        MarkReviewed = MarkReviewed == true,
    };
}

public sealed record CreateManualTradeRequest(
    Guid AccountId,
    string Symbol,
    TradeSide Side,
    DateTimeOffset OpenedAt,
    DateTimeOffset? ClosedAt,
    decimal EntryPrice,
    decimal? ExitPrice,
    decimal Quantity,
    int? Leverage,
    decimal? PositionMargin,
    decimal? Fees,
    decimal? Funding,
    decimal? StopLossPrice,
    decimal? TakeProfitPrice,
    Guid? StrategyId,
    string? Memo)
{
    public NewManualTrade ToSpec() => new()
    {
        AccountId = AccountId,
        Symbol = Symbol,
        Side = Side,
        OpenedAt = OpenedAt,
        ClosedAt = ClosedAt,
        EntryPrice = EntryPrice,
        ExitPrice = ExitPrice,
        Quantity = Quantity,
        Leverage = Leverage ?? 1,
        PositionMargin = PositionMargin,
        Fees = Fees ?? 0m,
        Funding = Funding ?? 0m,
        StopLossPrice = StopLossPrice,
        TakeProfitPrice = TakeProfitPrice,
        StrategyId = StrategyId,
        Memo = Memo,
    };
}
