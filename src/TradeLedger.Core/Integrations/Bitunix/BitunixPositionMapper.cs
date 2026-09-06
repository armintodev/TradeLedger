using TradeLedger.Core.Domain;
using TradeLedger.Core.Integrations.Bitunix.Dtos;

namespace TradeLedger.Core.Integrations.Bitunix;

public static class BitunixPositionMapper
{
    public static void ApplyTo(Trade trade, HistoryPositionDto dto, Guid accountId)
    {
        trade.AccountId = accountId;
        trade.Origin = TradeOrigin.Synced;
        trade.ExchangePositionId = dto.PositionId;
        trade.Symbol = dto.Symbol;
        trade.Side = ParseSide(dto.Side);
        trade.Quantity = dto.MaxQty;
        trade.EntryPrice = dto.EntryPrice;
        trade.ExitPrice = dto.ClosePrice > 0 ? dto.ClosePrice : null;
        trade.Leverage = dto.Leverage > 0 ? dto.Leverage : 1;
        trade.MarginMode = ParseMarginMode(dto.MarginMode);
        trade.PositionMode = ParsePositionMode(dto.PositionMode);
        trade.LiquidationPrice = dto.LiqPrice;
        trade.LiquidatedQuantity = dto.LiqQty > 0 ? dto.LiqQty : null;

        trade.Fees = Math.Abs(dto.Fee);

        trade.Funding = dto.Funding;

        trade.GrossProfitLoss = dto.RealizedPnl;

        if (dto.Ctime is { } ctime)
        {
            trade.OpenedAt = DateTimeOffset.FromUnixTimeMilliseconds(ctime);
            trade.OpenedAtRawMs = ctime;
        }

        if (dto.Mtime is { } mtime)
        {
            trade.ClosedAt = DateTimeOffset.FromUnixTimeMilliseconds(mtime);
            trade.ClosedAtRawMs = mtime;
        }

        if (trade.PositionMargin is null && trade.Leverage > 0 && trade.EntryPrice > 0)
        {
            trade.OrderValue = trade.EntryPrice * trade.Quantity;
            trade.PositionMargin = trade.OrderValue / trade.Leverage;
        }
    }

    public static void ApplyOpenTo(Trade trade, HistoryPositionDto dto, Guid accountId)
    {
        ApplyTo(trade, dto, accountId);
        trade.ClosedAt = null;
        trade.ClosedAtRawMs = null;
        trade.ExitPrice = null;
        trade.Outcome = TradeOutcome.Open;
    }

    public static Execution ToExecution(HistoryTradeDto dto, Trade trade)
    {
        var side = ParseSide(dto.Side);
        var isReduce = string.Equals(dto.ReduceOnly, "true", StringComparison.OrdinalIgnoreCase)
                       || side != trade.Side;

        return new Execution
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

    public static BalanceSnapshot ToSnapshot(FuturesAccountDto dto, Guid accountId, Guid userId)
    {
        var unrealized = dto.CrossUnrealizedPnl + dto.IsolationUnrealizedPnl;
        var wallet = dto.Available + dto.Frozen + dto.Margin;

        return new BalanceSnapshot
        {
            UserId = userId,
            AccountId = accountId,
            Asset = dto.MarginCoin,
            Available = dto.Available,
            Frozen = dto.Frozen,
            Margin = dto.Margin,
            WalletBalance = wallet,
            UnrealizedPnl = unrealized,

            Equity = wallet + unrealized,
            Bonus = dto.Bonus,
            CapturedAt = DateTimeOffset.UtcNow,
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
