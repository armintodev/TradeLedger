namespace TradeLedger.Core.MarketStructure;

public enum SwingType
{
    High = 1,
    Low = 2,
}

/// <summary>
/// A confirmed pivot: the bar where a leg turned, and the later bar where price had reversed
/// far enough to prove that it had. Both instants are bar open times, the way a chart names
/// a bar.
/// </summary>
/// <remarks>
/// <see cref="CandleIndex"/> always lies in the past relative to <see cref="ConfirmationIndex"/>,
/// which is both the point of this type and the trap in it. The pivot did not exist at
/// <see cref="CandleIndex"/> — nobody could know the leg had turned until
/// <see cref="ConfirmationIndex"/>. So anything reading swings while replaying bars must
/// filter on the confirmation index; <see cref="SwingSeries.KnownAt"/> is that filter.
/// </remarks>
public sealed record SwingPoint(
    SwingType Type,
    decimal Price,
    int CandleIndex,
    DateTimeOffset Time,
    int ConfirmationIndex,
    decimal ConfirmationPrice,
    DateTimeOffset ConfirmationTime,
    decimal Threshold,
    decimal Reversal)
{
    /// <summary>
    /// How many bars the pivot spent as a guess. Always at least one: the bar that sets an
    /// extreme is never the bar that confirms it.
    /// </summary>
    public int BarsToConfirm => ConfirmationIndex - CandleIndex;
}
