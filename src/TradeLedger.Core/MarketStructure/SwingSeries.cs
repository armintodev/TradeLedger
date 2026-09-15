namespace TradeLedger.Core.MarketStructure;

/// <summary>
/// The extreme of the leg still in progress: a candidate no reversal has confirmed yet, and
/// may never. Null until the detector has settled on a direction.
/// </summary>
public sealed record PendingSwing(
    SwingType Type,
    decimal Price,
    int CandleIndex,
    DateTimeOffset Time);

/// <summary>
/// Every swing point found over one candle range, in the order they were confirmed.
/// </summary>
/// <remarks>
/// <see cref="Points"/> is the after-the-fact view — right for analysis and for drawing on a
/// chart, wrong for anything replaying bars, because at any given bar it holds pivots that had
/// not been confirmed yet. Use <see cref="KnownAt"/> there.
///
/// The first point is anchored at <see cref="FirstDetectableIndex"/> whenever the range happens
/// to open at an extreme, which it usually does. That pivot is an artifact of where the data
/// starts rather than a turn the market made; it is identifiable as
/// <c>CandleIndex == FirstDetectableIndex</c>, and only the first point can be.
/// </remarks>
public sealed class SwingSeries
{
    private readonly SwingPoint[] _points;

    public SwingSeries(
        IEnumerable<SwingPoint> points,
        PendingSwing? pending,
        int firstDetectableIndex)
    {
        ArgumentNullException.ThrowIfNull(points);

        _points = points.ToArray();
        Pending = pending;
        FirstDetectableIndex = firstDetectableIndex;
    }

    /// <summary>
    /// Confirmed pivots ordered by confirmation, which is also order by pivot bar. Types
    /// strictly alternate.
    /// </summary>
    public IReadOnlyList<SwingPoint> Points => _points;

    public PendingSwing? Pending { get; }

    /// <summary>
    /// The first bar the detector could work with — where the reversal threshold became
    /// computable. Equal to the candle count when it never did.
    /// </summary>
    public int FirstDetectableIndex { get; }

    /// <summary>
    /// The pivots already confirmed at <paramref name="barIndex"/>: the only lookahead-free
    /// view of the series.
    /// </summary>
    public IReadOnlyList<SwingPoint> KnownAt(int barIndex) =>
        new ArraySegment<SwingPoint>(_points, 0, CountKnownAt(barIndex));

    private int CountKnownAt(int barIndex)
    {
        // Points are emitted in confirmation order, so ConfirmationIndex increases strictly
        // and the ones already known are always a prefix.
        var low = 0;
        var high = _points.Length;

        while (low < high)
        {
            var mid = low + ((high - low) / 2);

            if (_points[mid].ConfirmationIndex <= barIndex)
            {
                low = mid + 1;
            }
            else
            {
                high = mid;
            }
        }

        return low;
    }
}
