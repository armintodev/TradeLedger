using TradeLedger.Core.Domain;
using TradeLedger.Core.Integrations.Bitunix.Dtos;

namespace TradeLedger.Core.Integrations.Bitunix;

public static class BitunixPositionMapper
{
    public static ExchangePositionSnapshot ToSnapshot(HistoryPositionDto dto, bool stillOpen = false) =>
        new()
        {
            ExchangePositionId = dto.PositionId,
            Symbol = dto.Symbol,
            Side = ParseSide(dto.Side),
            Quantity = dto.MaxQty,
            EntryPrice = dto.EntryPrice,
            ExitPrice = dto.ClosePrice > 0 ? dto.ClosePrice : null,
            Leverage = dto.Leverage > 0 ? dto.Leverage : 1,
            MarginMode = ParseMarginMode(dto.MarginMode),
            PositionMode = ParsePositionMode(dto.PositionMode),
            LiquidationPrice = dto.LiqPrice,
            LiquidatedQuantity = dto.LiqQty > 0 ? dto.LiqQty : null,
            Fees = Math.Abs(dto.Fee),
            Funding = dto.Funding,
            GrossProfitLoss = dto.RealizedPnl,
            OpenedAt = dto.Ctime is { } ctime
                ? DateTimeOffset.FromUnixTimeMilliseconds(ctime)
                : null,
            OpenedAtRawMs = dto.Ctime,
            ClosedAt = dto.Mtime is { } mtime
                ? DateTimeOffset.FromUnixTimeMilliseconds(mtime)
                : null,
            ClosedAtRawMs = dto.Mtime,
            StillOpen = stillOpen,
        };

    public static NewExecution ToExecution(HistoryTradeDto dto, Trade trade)
    {
        var side = ParseSide(dto.Side);
        var isReduce = string.Equals(dto.ReduceOnly, "true", StringComparison.OrdinalIgnoreCase)
                       || side != trade.Side;

        return new NewExecution
        {
            UserId = trade.UserId,
            TradeId = trade.Id,
            AccountId = trade.AccountId,
            ExchangeTradeId = dto.TradeId,
            ExchangeOrderId = dto.OrderId,
            Side = side,
            Price = dto.Price,
            Quantity = dto.Qty,
            Fee = Math.Abs(dto.Fee),
            FeeAsset = dto.FeeCoin ?? "USDT",
            RealizedProfitLoss = dto.RealizedPnl,
            OrderType = ParseOrderType(dto.OrderType),
            Role = isReduce ? ExecutionRole.Reduce : ExecutionRole.Increase,
            ExecutedAt = dto.Ctime is { } ms
                ? DateTimeOffset.FromUnixTimeMilliseconds(ms)
                : DateTimeOffset.UtcNow,
            ExecutedAtRawMs = dto.Ctime,
        };
    }

    public static NewBalanceSnapshot ToSnapshot(FuturesAccountDto dto, Guid accountId, Guid userId)
    {
        var unrealized = dto.CrossUnrealizedPnl + dto.IsolationUnrealizedPnl;
        var wallet = dto.Available + dto.Frozen + dto.Margin;

        return new NewBalanceSnapshot
        {
            UserId = userId,
            AccountId = accountId,
            Asset = dto.MarginCoin,
            Available = dto.Available,
            Frozen = dto.Frozen,
            Margin = dto.Margin,
            WalletBalance = wallet,
            UnrealizedPnl = unrealized,
            Bonus = dto.Bonus,
            CapturedAt = DateTimeOffset.UtcNow,
            IsManual = false,
        };
    }

    private static TradeSide ParseSide(string? side) =>
        side?.ToUpperInvariant() switch
        {
            "SHORT" or "SELL" => TradeSide.Short,
            _ => TradeSide.Long,
        };

    private static MarginMode? ParseMarginMode(string? mode) =>
        mode?.ToUpperInvariant() switch
        {
            "ISOLATION" or "ISOLATED" => MarginMode.Isolated,
            "CROSS" => MarginMode.Cross,
            _ => null,
        };

    private static PositionMode? ParsePositionMode(string? mode) =>
        mode?.ToUpperInvariant() switch
        {
            "ONE_WAY" => PositionMode.OneWay,
            "HEDGE" => PositionMode.Hedge,
            _ => null,
        };

    private static OrderType ParseOrderType(string? type) =>
        type?.ToUpperInvariant() switch
        {
            "MARKET" => OrderType.Market,
            "LIMIT" => OrderType.Limit,
            _ => OrderType.Unknown,
        };
}
