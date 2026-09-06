namespace TradeLedger.Core.Domain;

public enum AccountKind
{
    ExchangeFutures = 1,
    ExchangeSpot = 2,
    ExchangeWallet = 3,
    ExternalWallet = 4,
    ManualVenue = 5,
}

public enum SyncMode
{
    Manual = 0,
    Api = 1,
}

public enum Venue
{
    Manual = 0,
    Bitunix = 1,
}

public enum TradeSide
{
    Long = 1,
    Short = 2,
}

public enum MarginMode
{
    Isolated = 1,
    Cross = 2,
}

public enum PositionMode
{
    OneWay = 1,
    Hedge = 2,
}

public enum TradeOrigin
{
    Manual = 0,
    Synced = 1,
}

public enum ReviewState
{
    Unreviewed = 0,
    Reviewed = 1,
}

public enum TradeOutcome
{
    Open = 0,
    Win = 1,
    Loss = 2,
    Breakeven = 3,
}

public enum ExecutionRole
{
    Open = 1,
    Increase = 2,
    Reduce = 3,
    Close = 4,
    Liquidation = 5,
}

public enum OrderType
{
    Unknown = 0,
    Market = 1,
    Limit = 2,
}

public enum PlanStatus
{
    Draft = 0,
    Active = 1,
    Linked = 2,
    Abandoned = 3,
    Expired = 4,
}

public enum TransferDirection
{
    Deposit = 1,
    Withdrawal = 2,
    Internal = 3,
}

public enum HoldingKind
{
    Spot = 1,
    Wallet = 2,
    LiquidityPool = 3,
    Farm = 4,
}

public enum TaxonomyKind
{
    Strategy = 1,
    MentalState = 2,
    Mistake = 3,
    Tracking = 4,
    ChecklistItem = 5,
    Timeframe = 6,
    ExitType = 7,
    EntryType = 8,
}

public enum SyncRunStatus
{
    Running = 0,
    Succeeded = 1,
    Failed = 2,
}
