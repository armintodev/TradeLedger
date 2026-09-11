namespace TradeLedger.Core.Domain.Backtesting;

public enum BacktestAccountMode
{
    Sequential = 1,
    Independent = 2,
}

public enum BacktestKind
{
    RuleEngine = 1,
    WhatIf = 2,
}

public enum BacktestStatus
{
    Queued = 0,
    Running = 1,
    Succeeded = 2,
    Failed = 3,
    Cancelled = 4,
}

public enum DataQuality
{
    Clean = 0,
    Gapped = 1,
}

public enum BacktestExitReason
{
    StopLoss = 1,
    TakeProfit = 2,
    Liquidation = 3,
    EndOfData = 4,
}

public enum IntrabarResolution
{
    Unambiguous = 0,
    ResolvedByMinute = 1,
    AssumedWithinMinute = 2,
    AssumedNoMinuteData = 3,
}
