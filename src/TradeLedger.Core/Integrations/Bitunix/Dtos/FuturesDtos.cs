using System.Text.Json.Serialization;

namespace TradeLedger.Core.Integrations.Bitunix.Dtos;

public sealed class BitunixResponse<T>
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("msg")]
    public string? Message { get; set; }

    [JsonPropertyName("data")]
    public T? Data { get; set; }

    public bool IsSuccess => Code == 0;
}

public sealed class HistoryPositionsPage
{
    [JsonPropertyName("positionList")]
    public List<HistoryPositionDto> PositionList { get; set; } = [];

    [JsonPropertyName("total")]
    public long? Total { get; set; }
}

public sealed class HistoryPositionDto
{
    public string PositionId { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;

    public decimal MaxQty { get; set; }

    public decimal EntryPrice { get; set; }

    public decimal ClosePrice { get; set; }

    public decimal LiqQty { get; set; }

    public string Side { get; set; } = string.Empty;

    public string? MarginMode { get; set; }

    public string? PositionMode { get; set; }

    public int Leverage { get; set; }

    public decimal Fee { get; set; }

    public decimal Funding { get; set; }

    [JsonPropertyName("realizedPNL")]
    public decimal RealizedPnl { get; set; }

    public decimal? LiqPrice { get; set; }

    public long? Ctime { get; set; }

    public long? Mtime { get; set; }

    public long? SubAccountId { get; set; }
}

public sealed class FuturesAccountDto
{
    public string MarginCoin { get; set; } = "USDT";

    public decimal Available { get; set; }

    public decimal Frozen { get; set; }

    public decimal Margin { get; set; }

    public decimal Transfer { get; set; }

    public string? PositionMode { get; set; }

    [JsonPropertyName("crossUnrealizedPNL")]
    public decimal CrossUnrealizedPnl { get; set; }

    [JsonPropertyName("isolationUnrealizedPNL")]
    public decimal IsolationUnrealizedPnl { get; set; }

    public decimal Bonus { get; set; }
}

public sealed class HistoryTradeDto
{
    public string TradeId { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public string? PositionId { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Side { get; set; } = string.Empty;

    public string? ReduceOnly { get; set; }

    public decimal Price { get; set; }
    public decimal Qty { get; set; }
    public decimal Fee { get; set; }
    public string? FeeCoin { get; set; }

    [JsonPropertyName("realizedPNL")]
    public decimal? RealizedPnl { get; set; }

    public string? OrderType { get; set; }
    public long? Ctime { get; set; }
}

public sealed class HistoryTradesPage
{
    [JsonPropertyName("tradeList")]
    public List<HistoryTradeDto> TradeList { get; set; } = [];

    [JsonPropertyName("total")]
    public long? Total { get; set; }
}
