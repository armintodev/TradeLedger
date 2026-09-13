using TradeLedger.Core.Domain;
using TradeLedger.Core.Domain.MarketData;

namespace TradeLedger.UnitTests.MarketData;

public class MarketDataBackfillJobTests
{
    private static readonly DateTimeOffset From = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset To = new(2025, 2, 1, 0, 0, 0, TimeSpan.Zero);

    /// A running job stops at its next chunk boundary and cancels itself there. A
    /// queued one never will — the worker's pickup query skips a job whose
    /// cancellation was requested — so it would sit on Queued for good.
    [Fact]
    public void CancellingAQueuedJobFinishesItImmediately()
    {
        var job = Queue();

        job.RequestCancellation();

        Assert.True(job.CancellationRequested);
        Assert.Equal(MarketDataJobStatus.Cancelled, job.Status);
        Assert.True(job.IsFinished);
        Assert.NotNull(job.FinishedAt);
    }

    [Fact]
    public void CancellingARunningJobOnlyFlagsIt()
    {
        var job = Queue();
        job.Start();

        job.RequestCancellation();

        Assert.True(job.CancellationRequested);
        Assert.Equal(MarketDataJobStatus.Running, job.Status);
        Assert.False(job.IsFinished);
    }

    [Fact]
    public void AFinishedJobHasNothingToCancel()
    {
        var job = Queue();
        job.Start();
        job.Succeed();

        var thrown = Assert.Throws<ResourceConflictException>(job.RequestCancellation);

        Assert.Equal("job_already_finished", thrown.Code);
    }

    private static MarketDataBackfillJob Queue() => MarketDataBackfillJob.Queue(
        CandleSource.BinanceFutures, "BTCUSDT", CandleInterval.OneHour, From, To);
}
