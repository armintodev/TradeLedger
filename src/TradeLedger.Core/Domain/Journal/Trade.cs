namespace TradeLedger.Core.Domain;

public sealed class Trade : IUserOwned
{
    private readonly List<Execution> _executions = [];
    private readonly List<TradeMistake> _mistakes = [];
    private readonly List<TradeTracking> _trackings = [];
    private readonly List<Attachment> _attachments = [];
    private readonly List<FundingPayment> _fundingPayments = [];

    private decimal? _balanceBefore;

    private Trade()
    {
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid UserId { get; private set; }

    public Guid AccountId { get; private set; }

    public Account? Account { get; private set; }

    public TradeOrigin Origin { get; private set; } = TradeOrigin.Manual;

    public ReviewState ReviewState { get; private set; } = ReviewState.Unreviewed;

    public string? ExchangePositionId { get; private set; }

    public string Symbol { get; private set; } = string.Empty;

    public TradeSide Side { get; private set; }

    public DateTimeOffset OpenedAt { get; private set; }

    public long? OpenedAtRawMs { get; private set; }

    public MarketSession MarketSession { get; private set; }

    public decimal EntryPrice { get; private set; }

    public decimal Quantity { get; private set; }

    public decimal? PositionMargin { get; private set; }

    public int Leverage { get; private set; } = 1;

    public decimal? OrderValue { get; private set; }

    public MarginMode? MarginMode { get; private set; }

    public PositionMode? PositionMode { get; private set; }

    public OrderType OrderType { get; private set; } = OrderType.Unknown;

    public DateTimeOffset? ClosedAt { get; private set; }

    public long? ClosedAtRawMs { get; private set; }

    public decimal? ExitPrice { get; private set; }

    public decimal? PercentClosed { get; private set; }

    public decimal? LiquidatedQuantity { get; private set; }

    public decimal? LiquidationPrice { get; private set; }

    public decimal? StopLossPrice { get; private set; }

    public decimal? TakeProfitPrice { get; private set; }

    public decimal? PositionToAccountPercent { get; private set; }

    public decimal? PlannedStopLossPercent { get; private set; }

    public decimal? AccountRiskedPercent { get; private set; }

    public decimal? PlannedReturnR { get; private set; }

    public decimal GrossProfitLoss { get; private set; }

    public decimal Fees { get; private set; }

    public decimal Funding { get; private set; }

    public decimal NetProfitLoss { get; private set; }

    public decimal? AchievedReturnR { get; private set; }

    public decimal? TradeGainPercent { get; private set; }

    public decimal? AccountChangePercent { get; private set; }

    public decimal? BalanceAfter { get; private set; }

    public TimeSpan? Duration { get; private set; }

    public TradeOutcome Outcome { get; private set; } = TradeOutcome.Open;

    public Guid? StrategyId { get; private set; }

    public TaxonomyTerm? Strategy { get; private set; }

    public Guid? TimeframeId { get; private set; }

    public TaxonomyTerm? Timeframe { get; private set; }

    public Guid? EntryTypeId { get; private set; }

    public TaxonomyTerm? EntryType { get; private set; }

    public Guid? ExitTypeId { get; private set; }

    public TaxonomyTerm? ExitType { get; private set; }

    public Guid? EntryMentalStateId { get; private set; }

    public TaxonomyTerm? EntryMentalState { get; private set; }

    public Guid? ExitMentalStateId { get; private set; }

    public TaxonomyTerm? ExitMentalState { get; private set; }

    public MarketContext? MarketContext { get; private set; }

    public int? Rating { get; private set; }

    public string? Memo { get; private set; }

    public string? Tag { get; private set; }

    public string? PostTradeTag { get; private set; }

    public Guid? TradePlanId { get; private set; }

    public TradePlan? TradePlan { get; private set; }

    public bool IsPlanned { get; private set; }

    public IReadOnlyCollection<Execution> Executions => _executions;

    public IReadOnlyCollection<TradeMistake> Mistakes => _mistakes;

    public IReadOnlyCollection<TradeTracking> Trackings => _trackings;

    public IReadOnlyCollection<Attachment> Attachments => _attachments;

    public IReadOnlyCollection<FundingPayment> FundingPayments => _fundingPayments;

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;

    public bool IsClosed => ClosedAt is not null;

    public bool IsSynced => Origin == TradeOrigin.Synced;

    public bool IsReviewed => ReviewState == ReviewState.Reviewed;

    public bool NeedsReview => IsClosed && !IsReviewed;

    public static Trade OpenManual(NewManualTrade spec)
    {
        var trade = new Trade
        {
            UserId = spec.UserId,
            AccountId = Guard.NotEmpty(spec.AccountId, nameof(spec.AccountId)),
            Symbol = Guard.NotBlank(spec.Symbol, nameof(spec.Symbol)).ToUpperInvariant(),
            Side = spec.Side,
            Origin = TradeOrigin.Manual,
            OpenedAt = Guard.NotDefault(spec.OpenedAt, nameof(spec.OpenedAt)),
            EntryPrice = Guard.Positive(spec.EntryPrice, nameof(spec.EntryPrice)),
            Quantity = Guard.Positive(spec.Quantity, nameof(spec.Quantity)),
            Leverage = Guard.InRange(spec.Leverage, 1, 500, nameof(spec.Leverage)),
            PositionMargin = Guard.PositiveOrNull(spec.PositionMargin, nameof(spec.PositionMargin)),
            Fees = Guard.NotNegative(spec.Fees, nameof(spec.Fees)),
            Funding = spec.Funding,
            StrategyId = spec.StrategyId,
            Memo = spec.Memo,
        };

        if (spec.ExitPrice is { } exit)
        {
            trade.ExitPrice = Guard.Positive(exit, nameof(spec.ExitPrice));
        }

        if (spec.ClosedAt is { } closed)
        {
            Guard.Rule(
                closed >= trade.OpenedAt,
                "closed_before_opened",
                "A trade cannot close before it opened.");

            trade.ClosedAt = closed;
        }

        trade.ApplyStopAndTarget(spec.StopLossPrice, spec.TakeProfitPrice);

        trade.OrderValue = trade.EntryPrice * trade.Quantity;
        trade.PositionMargin ??= trade.OrderValue / trade.Leverage;

        if (trade.ExitPrice is { } exitPrice)
        {
            var direction = trade.Side == TradeSide.Long ? 1m : -1m;
            trade.GrossProfitLoss = (exitPrice - trade.EntryPrice) * trade.Quantity * direction;
        }

        trade.Recalculate();

        return trade;
    }

    public static Trade FromExchange(Guid userId, Guid accountId, ExchangePositionSnapshot snapshot)
    {
        var trade = new Trade
        {
            UserId = userId,
            AccountId = Guard.NotEmpty(accountId, nameof(accountId)),
            Origin = TradeOrigin.Synced,
            ReviewState = ReviewState.Unreviewed,
            Symbol = Guard.NotBlank(snapshot.Symbol, nameof(snapshot.Symbol)),
        };

        trade.ApplyExchangeSnapshot(snapshot);

        return trade;
    }

    public void ApplyExchangeSnapshot(ExchangePositionSnapshot snapshot)
    {
        Guard.Rule(
            Origin == TradeOrigin.Synced,
            "manual_trade_from_exchange",
            "A manual trade cannot be overwritten by an exchange snapshot.");

        ExchangePositionId = Guard.NotBlank(
            snapshot.ExchangePositionId,
            nameof(snapshot.ExchangePositionId));

        Symbol = Guard.NotBlank(snapshot.Symbol, nameof(snapshot.Symbol));
        Side = snapshot.Side;
        Quantity = snapshot.Quantity;
        EntryPrice = snapshot.EntryPrice;
        Leverage = snapshot.Leverage > 0 ? snapshot.Leverage : 1;
        MarginMode = snapshot.MarginMode;
        PositionMode = snapshot.PositionMode;
        LiquidationPrice = snapshot.LiquidationPrice;
        LiquidatedQuantity = snapshot.LiquidatedQuantity;
        Fees = snapshot.Fees;
        Funding = snapshot.Funding;
        GrossProfitLoss = snapshot.GrossProfitLoss;

        if (snapshot.OpenedAt is { } openedAt)
        {
            OpenedAt = openedAt;
            OpenedAtRawMs = snapshot.OpenedAtRawMs;
        }

        if (snapshot.StillOpen)
        {
            ClosedAt = null;
            ClosedAtRawMs = null;
            ExitPrice = null;
        }
        else
        {
            ExitPrice = snapshot.ExitPrice;

            if (snapshot.ClosedAt is { } closedAt)
            {
                ClosedAt = closedAt;
                ClosedAtRawMs = snapshot.ClosedAtRawMs;
            }
        }

        if (PositionMargin is null && Leverage > 0 && EntryPrice > 0)
        {
            OrderValue = EntryPrice * Quantity;
            PositionMargin = OrderValue / Leverage;
        }

        Recalculate();
    }

    public void ApplyBalanceContext(decimal? balanceBeforeTrade)
    {
        _balanceBefore = balanceBeforeTrade;
        Recalculate();
    }

    public void Journal(TradeJournalEdit edit)
    {
        StrategyId = edit.StrategyId ?? StrategyId;
        TimeframeId = edit.TimeframeId ?? TimeframeId;
        EntryTypeId = edit.EntryTypeId ?? EntryTypeId;
        ExitTypeId = edit.ExitTypeId ?? ExitTypeId;
        EntryMentalStateId = edit.EntryMentalStateId ?? EntryMentalStateId;
        ExitMentalStateId = edit.ExitMentalStateId ?? ExitMentalStateId;
        Memo = edit.Memo ?? Memo;
        Tag = edit.Tag ?? Tag;
        PostTradeTag = edit.PostTradeTag ?? PostTradeTag;

        if (edit.Rating is { } rating)
        {
            Rating = Guard.InRange(rating, 1, 5, nameof(edit.Rating));
        }

        if (edit.MarketContext is not null)
        {
            MarketContext = edit.MarketContext;
        }

        if (edit.StopLossPrice is not null || edit.TakeProfitPrice is not null)
        {
            ApplyStopAndTarget(
                edit.StopLossPrice ?? StopLossPrice,
                edit.TakeProfitPrice ?? TakeProfitPrice);
        }

        if (edit.MarkReviewed)
        {
            MarkReviewed();
        }

        Recalculate();
    }

    public void SetRiskLevels(decimal? stopLossPrice, decimal? takeProfitPrice)
    {
        ApplyStopAndTarget(stopLossPrice, takeProfitPrice);
        Recalculate();
    }

    public void MarkReviewed()
    {
        Guard.Rule(
            IsClosed,
            "review_open_trade",
            "An open trade cannot be reviewed; it has no outcome yet.");

        ReviewState = ReviewState.Reviewed;
        Touch();
    }

    public void ReopenForReview()
    {
        ReviewState = ReviewState.Unreviewed;
        Touch();
    }

    public void ReplaceMistakes(IEnumerable<Guid> taxonomyTermIds)
    {
        _mistakes.Clear();

        foreach (var termId in taxonomyTermIds.Distinct())
        {
            _mistakes.Add(TradeMistake.For(this, termId));
        }

        Touch();
    }

    public void ReplaceTrackings(IEnumerable<Guid> taxonomyTermIds)
    {
        _trackings.Clear();

        foreach (var termId in taxonomyTermIds.Distinct())
        {
            _trackings.Add(TradeTracking.For(this, termId));
        }

        Touch();
    }

    public void AdoptPlan(TradePlan plan)
    {
        Guard.Rule(
            plan.UserId == UserId,
            "plan_owner_mismatch",
            "A trade can only be linked to a plan belonging to the same user.");

        TradePlanId = plan.Id;
        IsPlanned = true;

        StrategyId ??= plan.StrategyId;
        TimeframeId ??= plan.TimeframeId;
        EntryMentalStateId ??= plan.EntryMentalStateId;
        StopLossPrice ??= plan.PlannedStopLossPrice;
        TakeProfitPrice ??= plan.PlannedTakeProfitPrice;
        PlannedReturnR ??= plan.PlannedRiskReward;
        MarketContext ??= plan.MarketContext;

        Recalculate();
    }

    public void MarkUnplanned()
    {
        IsPlanned = false;
        Touch();
    }

    public void AddExecution(Execution execution)
    {
        Guard.Rule(
            execution.TradeId == Id,
            "execution_trade_mismatch",
            "An execution must belong to the trade it is added to.");

        _executions.Add(execution);
        Touch();
    }

    public void EnsureDeletable()
    {
        if (IsSynced)
        {
            throw new ResourceConflictException(
                "synced_trade_immutable",
                "This trade came from the exchange and would return on the next sync.");
        }
    }

    private void ApplyStopAndTarget(decimal? stopLossPrice, decimal? takeProfitPrice)
    {
        if (stopLossPrice is { } stop)
        {
            Guard.Positive(stop, nameof(stopLossPrice));

            var stopIsOnTheLosingSide = Side == TradeSide.Long
                ? stop < EntryPrice
                : stop > EntryPrice;

            Guard.Rule(
                stopIsOnTheLosingSide,
                "stop_on_wrong_side",
                Side == TradeSide.Long
                    ? "A long stop loss must sit below the entry price."
                    : "A short stop loss must sit above the entry price.");
        }

        if (takeProfitPrice is { } target)
        {
            Guard.Positive(target, nameof(takeProfitPrice));

            var targetIsOnTheWinningSide = Side == TradeSide.Long
                ? target > EntryPrice
                : target < EntryPrice;

            Guard.Rule(
                targetIsOnTheWinningSide,
                "target_on_wrong_side",
                Side == TradeSide.Long
                    ? "A long take profit must sit above the entry price."
                    : "A short take profit must sit below the entry price.");
        }

        StopLossPrice = stopLossPrice;
        TakeProfitPrice = takeProfitPrice;
    }

    private void Recalculate()
    {
        NetProfitLoss = GrossProfitLoss - Fees + Funding;

        MarketSession = MarketSessionCalendar.At(OpenedAt);

        if (ClosedAt is null)
        {
            Outcome = TradeOutcome.Open;
            Duration = null;
        }
        else
        {
            Duration = ClosedAt.Value - OpenedAt;
            Outcome = NetProfitLoss switch
            {
                > 0 => TradeOutcome.Win,
                < 0 => TradeOutcome.Loss,
                _ => TradeOutcome.Breakeven,
            };
        }

        var riskPerUnit = StopLossPrice is { } sl && sl > 0
            ? Math.Abs(EntryPrice - sl)
            : (decimal?)null;

        if (riskPerUnit is { } rpu && rpu > 0 && Quantity > 0)
        {
            var riskAmount = rpu * Quantity;
            if (riskAmount > 0)
            {
                AchievedReturnR = decimal.Round(NetProfitLoss / riskAmount, 6);
            }
        }

        if (PositionMargin is { } margin && margin > 0)
        {
            TradeGainPercent = decimal.Round(NetProfitLoss / margin * 100m, 6);
        }

        if (_balanceBefore is { } bb && bb > 0)
        {
            AccountChangePercent = decimal.Round(NetProfitLoss / bb * 100m, 6);
            BalanceAfter = bb + NetProfitLoss;

            if (OrderValue is { } ov)
            {
                PositionToAccountPercent = decimal.Round(ov / bb * 100m, 6);
            }

            if (riskPerUnit is { } r && Quantity > 0)
            {
                AccountRiskedPercent = decimal.Round(r * Quantity / bb * 100m, 6);
            }
        }

        if (StopLossPrice is { } stop && stop > 0 && EntryPrice > 0)
        {
            PlannedStopLossPercent = decimal.Round(Math.Abs(EntryPrice - stop) / EntryPrice * 100m, 6);
        }

        Touch();
    }

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;
}
