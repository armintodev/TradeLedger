using TradeLedger.Core.Analytics;
using TradeLedger.Core.Backtesting.Indicators;
using TradeLedger.Core.Backtesting.Rules;
using TradeLedger.Core.Domain;
using TradeLedger.Core.Domain.Backtesting;
using TradeLedger.Core.Domain.MarketData;
using TradeLedger.Core.MarketData;

namespace TradeLedger.Core.Backtesting.Engine;

public sealed record SimulationParameters(
    string Symbol,
    DateTimeOffset From,
    decimal OpeningBalance,
    decimal RiskPercentPerPosition,
    decimal RiskRewardRatio,
    int Leverage,
    decimal MaintenanceMarginRate,
    CostModel Costs);

public interface IMinuteCandleSource
{
    Task<IReadOnlyList<Candle>> GetAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct);
}

public sealed class NoMinuteCandles : IMinuteCandleSource
{
    public static readonly NoMinuteCandles Instance = new();

    public Task<IReadOnlyList<Candle>> GetAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Candle>>([]);
}

public sealed class NoProgress : IBacktestProgress
{
    public static readonly NoProgress Instance = new();

    public Task ReportAsync(int barsProcessed, int totalBars, CancellationToken ct) =>
        Task.CompletedTask;

    public Task<bool> IsCancellationRequestedAsync(CancellationToken ct) =>
        Task.FromResult(false);
}

public static class RuleSimulationCore
{
    public static async Task<BacktestEngineResult> RunAsync(
        RuleDocument document,
        IReadOnlyList<Candle> bars,
        int startIndex,
        SimulationParameters parameters,
        IReadOnlyList<FundingRateHistory> funding,
        IMinuteCandleSource minutes,
        IBacktestProgress progress,
        List<string> warnings,
        CancellationToken ct = default)
    {
        var series = new Dictionary<string, IndicatorSeries>(StringComparer.OrdinalIgnoreCase);

        foreach (var spec in document.Indicators)
        {
            series[spec.Id] = IndicatorFactory.Compute(spec.Type, spec.Period, spec.Source, bars);
        }

        var window = new BarWindow(bars, series);
        var state = new SimulationState(parameters.OpeningBalance);
        var totalBars = bars.Count - startIndex;
        var interval = bars[0].Interval;

        for (var i = startIndex; i < bars.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            if (i % 250 == 0)
            {
                if (await progress.IsCancellationRequestedAsync(ct).ConfigureAwait(false))
                {
                    throw new BacktestCancelledException();
                }

                await progress.ReportAsync(i - startIndex, totalBars, ct).ConfigureAwait(false);
            }

            var bar = bars[i];
            window.MoveTo(i);

            if (state.Pending is { } pending && state.Open is null)
            {
                TryOpenPosition(parameters, state, pending, bar, i, series, document);
                state.Pending = null;
            }

            if (state.Open is not null)
            {
                AccrueFunding(state, bar, funding, interval);
                TrackExcursion(state, bar);

                await TryCloseAsync(
                    state, bar, i, minutes, interval, parameters, ct).ConfigureAwait(false);
            }

            state.TrackIntrabarDrawdown(bar);

            if (state.Open is null && state.Pending is null)
            {
                EvaluateEntry(document, window, state, i);
            }
        }

        if (state.Open is not null)
        {
            Close(
                state, state.Open, bars[^1].Close, bars[^1], bars.Count - 1,
                BacktestExitReason.EndOfData, IntrabarResolution.Unambiguous, parameters);

            state.OpenAtEndOfData++;
        }

        await progress.ReportAsync(totalBars, totalBars, ct).ConfigureAwait(false);

        return Build(parameters, state, warnings);
    }

    private static void EvaluateEntry(
        RuleDocument document,
        BarWindow window,
        SimulationState state,
        int index)
    {
        var longSignal = document.Entry.Long is not null
                         && RuleEvaluator.Evaluate(document.Entry.Long, window);

        var shortSignal = document.Entry.Short is not null
                          && RuleEvaluator.Evaluate(document.Entry.Short, window);

        if (longSignal && shortSignal)
        {
            state.AmbiguousSignals++;
            return;
        }

        if (longSignal)
        {
            state.Pending = new PendingEntry(TradeSide.Long, index);
        }
        else if (shortSignal)
        {
            state.Pending = new PendingEntry(TradeSide.Short, index);
        }
    }

    private static void TryOpenPosition(
        SimulationParameters parameters,
        SimulationState state,
        PendingEntry pending,
        Candle bar,
        int index,
        IReadOnlyDictionary<string, IndicatorSeries> series,
        RuleDocument document)
    {
        var side = pending.Side;
        var fill = parameters.Costs.EntryFillPrice(side, bar.Open);
        var stop = ResolveStop(document.StopLoss, side, fill, pending.SignalBarIndex, series);

        if (stop is not { } stopPrice
            || !PositionSizer.IsStopOnTheCorrectSide(side, fill, stopPrice))
        {
            state.SkippedInvalidStop++;
            return;
        }

        var sizing = PositionSizer.Size(
            state.Equity,
            parameters.RiskPercentPerPosition,
            side,
            fill,
            stopPrice,
            parameters.Leverage);

        if (!sizing.Accepted)
        {
            if (sizing.Refusal == SizingRefusal.InsufficientMargin)
            {
                state.SkippedInsufficientMargin++;
            }
            else
            {
                state.SkippedInvalidStop++;
            }

            return;
        }

        var size = sizing.Size!;

        state.Open = new OpenPosition(
            side,
            fill,
            size.Quantity,
            size.Notional,
            size.Margin,
            stopPrice,
            PositionSizer.TakeProfitPrice(side, fill, stopPrice, parameters.RiskRewardRatio),
            LiquidationModel.LiquidationPrice(
                side, fill, parameters.Leverage, parameters.MaintenanceMarginRate),
            size.RiskAmount,
            index,
            bar.OpenTime,
            parameters.Costs.TakerFee(size.Notional));
    }

    private static decimal? ResolveStop(
        StopLossRule rule,
        TradeSide side,
        decimal entryPrice,
        int signalBarIndex,
        IReadOnlyDictionary<string, IndicatorSeries> series)
    {
        switch (rule)
        {
            case PercentStop percent:
                return side == TradeSide.Long
                    ? entryPrice * (1m - percent.Percent / 100m)
                    : entryPrice * (1m + percent.Percent / 100m);

            case IndicatorLevelStop level:
                {
                    if (!series.TryGetValue(level.Ref, out var indicator))
                    {
                        return null;
                    }

                    var value = indicator.At(signalBarIndex - level.Offset, level.Output);

                    if (value is not { } raw || raw <= 0)
                    {
                        return null;
                    }

                    return side == TradeSide.Long
                        ? raw * (1m - level.BufferPercent / 100m)
                        : raw * (1m + level.BufferPercent / 100m);
                }

            default:
                return null;
        }
    }

    private static async Task TryCloseAsync(
        SimulationState state,
        Candle bar,
        int index,
        IMinuteCandleSource minutes,
        CandleInterval interval,
        SimulationParameters parameters,
        CancellationToken ct)
    {
        var position = state.Open!;
        var levels = new IntrabarLevels(position.Stop, position.Target, position.Liquidation);

        var quick = IntrabarResolver.Resolve(bar, position.Side, levels);

        if (quick is null)
        {
            return;
        }

        var outcome = quick.Resolution == IntrabarResolution.Unambiguous
            ? quick
            : IntrabarResolver.Resolve(
                bar,
                position.Side,
                levels,
                await minutes
                    .GetAsync(bar.OpenTime, bar.OpenTime + interval.Duration(), ct)
                    .ConfigureAwait(false));

        if (outcome is null)
        {
            return;
        }

        var level = IntrabarResolver.FillPrice(outcome.Level, position.Side, levels, bar);

        var exitPrice = outcome.Level switch
        {
            IntrabarLevel.Stop => parameters.Costs.StopFillPrice(position.Side, level),
            IntrabarLevel.Target => parameters.Costs.TakeProfitFillPrice(level),
            _ => parameters.Costs.LiquidationFillPrice(level),
        };

        Close(
            state, position, exitPrice, bar, index, outcome.Level.ToExitReason(),
            outcome.Resolution, parameters);
    }

    private static void Close(
        SimulationState state,
        OpenPosition position,
        decimal exitPrice,
        Candle bar,
        int index,
        BacktestExitReason reason,
        IntrabarResolution resolution,
        SimulationParameters parameters)
    {
        var gross = CostModel.GrossProfitLoss(
            position.Side, position.EntryPrice, exitPrice, position.Quantity);

        var exitFee = parameters.Costs.TakerFee(exitPrice * position.Quantity);
        var fees = position.EntryFee + exitFee;
        var net = gross - fees + position.Funding;

        state.Equity += net;

        var closedAt = bar.OpenTime + bar.Interval.Duration();

        var trade = BacktestTrade.Close(new ClosedBacktestPosition
        {
            Sequence = state.Trades.Count + 1,
            Symbol = parameters.Symbol,
            Side = position.Side,
            OpenedAt = position.OpenedAt,
            ClosedAt = closedAt,
            EntryBarIndex = position.EntryBarIndex,
            ExitBarIndex = index,
            EntryPrice = position.EntryPrice,
            ExitPrice = exitPrice,
            Quantity = position.Quantity,
            Leverage = parameters.Leverage,
            PositionMargin = position.Margin,
            OrderValue = position.Notional,
            StopLossPrice = position.Stop,
            TakeProfitPrice = position.Target,
            LiquidationPrice = position.Liquidation,
            GrossProfitLoss = gross,
            Fees = fees,
            Funding = position.Funding,
            RiskAmount = position.RiskAmount,
            MaxAdverse = position.MaxAdverse,
            MaxFavourable = position.MaxFavourable,
            BalanceAfter = state.Equity,
            PlannedReturnR = parameters.RiskRewardRatio,
            ExitReason = reason,
            IntrabarResolution = resolution,
        });

        trade.RecordExecution(
            ExecutionRole.Open,
            position.EntryPrice,
            position.Quantity,
            position.EntryFee,
            position.OpenedAt,
            position.EntryBarIndex);

        trade.RecordExecution(
            reason == BacktestExitReason.Liquidation
                ? ExecutionRole.Liquidation
                : ExecutionRole.Close,
            exitPrice,
            position.Quantity,
            exitFee,
            closedAt,
            index);

        state.Trades.Add(trade);
        state.CountResolution(resolution);

        state.EquityMarks.Add((closedAt, state.Equity));

        state.Open = null;
    }

    private static void AccrueFunding(
        SimulationState state,
        Candle bar,
        IReadOnlyList<FundingRateHistory> rates,
        CandleInterval interval)
    {
        if (rates.Count == 0)
        {
            return;
        }

        var position = state.Open!;
        var barEnd = bar.OpenTime + interval.Duration();

        foreach (var rate in rates)
        {
            if (rate.FundingTime < bar.OpenTime
                || rate.FundingTime >= barEnd
                || rate.FundingTime < position.OpenedAt)
            {
                continue;
            }

            position.Funding += CostModel.FundingPayment(
                position.Side, position.Notional, rate.FundingRate);
        }
    }

    private static void TrackExcursion(SimulationState state, Candle bar)
    {
        var position = state.Open!;

        var adverse = position.Side == TradeSide.Long ? bar.Low : bar.High;
        var favourable = position.Side == TradeSide.Long ? bar.High : bar.Low;

        var adverseMove = CostModel.GrossProfitLoss(
            position.Side, position.EntryPrice, adverse, position.Quantity);

        var favourableMove = CostModel.GrossProfitLoss(
            position.Side, position.EntryPrice, favourable, position.Quantity);

        if (-adverseMove > position.MaxAdverse)
        {
            position.MaxAdverse = -adverseMove;
        }

        if (favourableMove > position.MaxFavourable)
        {
            position.MaxFavourable = favourableMove;
        }
    }

    private static BacktestEngineResult Build(
        SimulationParameters parameters,
        SimulationState state,
        List<string> warnings)
    {
        state.EquityMarks.Insert(0, (parameters.From, parameters.OpeningBalance));

        var peak = 0m;
        var equityPoints = new List<BacktestEquityPoint>(state.EquityMarks.Count);

        for (var i = 0; i < state.EquityMarks.Count; i++)
        {
            var (at, equity) = state.EquityMarks[i];

            peak = Math.Max(peak, equity);
            equityPoints.Add(BacktestEquityPoint.Mark(i, at, equity, peak));
        }

        var assumed = state.AssumedNoMinuteData + state.AssumedWithinMinute;

        if (state.Trades.Count > 0 && assumed * 2 > state.Trades.Count)
        {
            warnings.Add(
                $"{assumed} of {state.Trades.Count} exits were assumed rather than resolved: the bar " +
                "held both the stop and the target and one-minute data could not separate them. " +
                "Backfill one-minute candles for this range to get a trustworthy result.");
        }

        var summary = new BacktestResultSummary
        {
            Performance = PerformanceMetrics.Compute(
                [.. state.Trades.Select(BacktestAccountService.ToMetricInput)]),
            MaxIntrabarDrawdown = state.MaxIntrabarDrawdown,
            MaxIntrabarDrawdownPercent = state.MaxIntrabarDrawdownPercent,
            AmbiguousSignals = state.AmbiguousSignals,
            SkippedInvalidStop = state.SkippedInvalidStop,
            SkippedInsufficientMargin = state.SkippedInsufficientMargin,
            OpenAtEndOfData = state.OpenAtEndOfData,
            ResolvedUnambiguous = state.ResolvedUnambiguous,
            ResolvedByMinute = state.ResolvedByMinute,
            AssumedWithinMinute = state.AssumedWithinMinute,
            AssumedNoMinuteData = state.AssumedNoMinuteData,
        };

        return new BacktestEngineResult(
            state.Trades, equityPoints, state.Equity, summary, warnings);
    }

    private sealed record PendingEntry(TradeSide Side, int SignalBarIndex);

    private sealed class OpenPosition(
        TradeSide side,
        decimal entryPrice,
        decimal quantity,
        decimal notional,
        decimal margin,
        decimal stop,
        decimal target,
        decimal liquidation,
        decimal riskAmount,
        int entryBarIndex,
        DateTimeOffset openedAt,
        decimal entryFee)
    {
        public TradeSide Side { get; } = side;
        public decimal EntryPrice { get; } = entryPrice;
        public decimal Quantity { get; } = quantity;
        public decimal Notional { get; } = notional;
        public decimal Margin { get; } = margin;
        public decimal Stop { get; } = stop;
        public decimal Target { get; } = target;
        public decimal Liquidation { get; } = liquidation;
        public decimal RiskAmount { get; } = riskAmount;
        public int EntryBarIndex { get; } = entryBarIndex;
        public DateTimeOffset OpenedAt { get; } = openedAt;
        public decimal EntryFee { get; } = entryFee;

        public decimal Funding { get; set; }
        public decimal MaxAdverse { get; set; }
        public decimal MaxFavourable { get; set; }
    }

    private sealed class SimulationState(decimal openingBalance)
    {
        private decimal _peak = openingBalance;

        public decimal Equity { get; set; } = openingBalance;

        public OpenPosition? Open { get; set; }

        public PendingEntry? Pending { get; set; }

        public List<BacktestTrade> Trades { get; } = [];

        public List<(DateTimeOffset At, decimal Equity)> EquityMarks { get; } = [];

        public int AmbiguousSignals { get; set; }
        public int SkippedInvalidStop { get; set; }
        public int SkippedInsufficientMargin { get; set; }
        public int OpenAtEndOfData { get; set; }

        public int ResolvedUnambiguous { get; private set; }
        public int ResolvedByMinute { get; private set; }
        public int AssumedWithinMinute { get; private set; }
        public int AssumedNoMinuteData { get; private set; }

        public decimal MaxIntrabarDrawdown { get; private set; }
        public decimal MaxIntrabarDrawdownPercent { get; private set; }

        public void CountResolution(IntrabarResolution resolution)
        {
            switch (resolution)
            {
                case IntrabarResolution.Unambiguous:
                    ResolvedUnambiguous++;
                    break;
                case IntrabarResolution.ResolvedByMinute:
                    ResolvedByMinute++;
                    break;
                case IntrabarResolution.AssumedWithinMinute:
                    AssumedWithinMinute++;
                    break;
                default:
                    AssumedNoMinuteData++;
                    break;
            }
        }

        public void TrackIntrabarDrawdown(Candle bar)
        {
            var marked = Equity;

            if (Open is { } position)
            {
                var adverse = position.Side == TradeSide.Long ? bar.Low : bar.High;

                marked += CostModel.GrossProfitLoss(
                    position.Side, position.EntryPrice, adverse, position.Quantity);
            }

            _peak = Math.Max(_peak, Math.Max(Equity, marked));

            var drawdown = _peak - marked;

            if (drawdown > MaxIntrabarDrawdown)
            {
                MaxIntrabarDrawdown = drawdown;
                MaxIntrabarDrawdownPercent = _peak > 0
                    ? decimal.Round(drawdown / _peak * 100m, 6)
                    : 0m;
            }
        }
    }
}
