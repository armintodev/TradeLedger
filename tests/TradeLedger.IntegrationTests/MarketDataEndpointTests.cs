using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TradeLedger.Core.Domain.MarketData;
using TradeLedger.Core.MarketData;

namespace TradeLedger.IntegrationTests;

[Collection(nameof(PostgresCollection))]
public sealed class MarketDataEndpointTests(PostgresFixture fixture) : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static readonly DateTimeOffset RangeStart = new(2025, 4, 1, 0, 0, 0, TimeSpan.Zero);

    private ApiFactory _factory = null!;

    public Task InitializeAsync()
    {
        _factory = new ApiFactory(fixture.ConnectionString);

        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    /// Without a list, a backfill is reachable only by an id the client happened to
    /// keep — which means no history at all once the tab is closed, including for the
    /// failed job whose error string is the only account of why it failed.
    [Fact]
    public async Task QueuedBackfillsComeBackAsHistoryNewestFirst()
    {
        var (_, token) = await _factory.CreateUserAsync();
        var client = _factory.CreateClientFor(token);

        var first = NewSymbol();
        var second = NewSymbol();

        var firstId = await QueueBackfillAsync(client, first);
        var secondId = await QueueBackfillAsync(client, second);

        var all = await ReadJsonAsync(await client.GetAsync("/api/market-data/backfill"));

        Assert.True(all.GetArrayLength() >= 2);
        Assert.Equal(secondId, all[0].GetProperty("id").GetGuid());
        Assert.Equal(firstId, all[1].GetProperty("id").GetGuid());

        var filtered = await ReadJsonAsync(
            await client.GetAsync($"/api/market-data/backfill?symbol={first.ToLowerInvariant()}"));

        Assert.Equal(1, filtered.GetArrayLength());
        Assert.Equal(firstId, filtered[0].GetProperty("id").GetGuid());

        var queued = await ReadJsonAsync(
            await client.GetAsync("/api/market-data/backfill?status=Queued"));

        Assert.Contains(
            queued.EnumerateArray(),
            job => job.GetProperty("id").GetGuid() == firstId);
    }

    /// A list is per user like every other IUserOwned query, so one trader's backfill
    /// history is not another's.
    [Fact]
    public async Task BackfillHistoryIsScopedToItsOwner()
    {
        var (_, mine) = await _factory.CreateUserAsync();
        var (_, theirs) = await _factory.CreateUserAsync();

        var symbol = NewSymbol();
        var jobId = await QueueBackfillAsync(_factory.CreateClientFor(mine), symbol);

        var otherView = await ReadJsonAsync(
            await _factory.CreateClientFor(theirs).GetAsync("/api/market-data/backfill"));

        Assert.DoesNotContain(
            otherView.EnumerateArray(),
            job => job.GetProperty("id").GetGuid() == jobId);
    }

    /// The worker's pickup query skips a job whose cancellation was requested, so a
    /// queued job asked to stop would otherwise sit on Queued forever with nothing in
    /// the response explaining why.
    [Fact]
    public async Task CancellingAQueuedBackfillFinishesItRatherThanStrandingIt()
    {
        var (_, token) = await _factory.CreateUserAsync();
        var client = _factory.CreateClientFor(token);

        var jobId = await QueueBackfillAsync(client, NewSymbol());

        var cancelled = await ReadJsonAsync(
            await client.PostAsync($"/api/market-data/backfill/{jobId}/cancel", null));

        Assert.True(cancelled.GetProperty("cancellationRequested").GetBoolean());
        Assert.Equal("Cancelled", cancelled.GetProperty("status").GetString());

        var again = await client.PostAsync($"/api/market-data/backfill/{jobId}/cancel", null);

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task StoredCandlesComeBackAsBarsWhenTheRangeFitsTheBudget()
    {
        var (_, token) = await _factory.CreateUserAsync();
        var client = _factory.CreateClientFor(token);

        var symbol = await SeedHourlyCandlesAsync(RangeStart, hours: 48);

        var series = await ReadJsonAsync(
            await client.GetAsync(CandlesUrl(symbol, RangeStart, RangeStart.AddHours(47))));

        Assert.Equal(48, series.GetProperty("total").GetInt32());
        Assert.Equal(48, series.GetProperty("returned").GetInt32());
        Assert.Equal(1, series.GetProperty("bucketSize").GetInt32());
        Assert.False(series.GetProperty("isDownsampled").GetBoolean());

        var bars = series.GetProperty("candles");

        Assert.Equal(48, bars.GetArrayLength());
        Assert.Equal(100m, bars[0].GetProperty("open").GetDecimal());
        Assert.Equal(147.5m, bars[47].GetProperty("close").GetDecimal());
    }

    /// Past the point budget the range is aggregated, not truncated: truncating would
    /// silently hide the far end of the range, which is exactly where a gap tends to be.
    [Fact]
    public async Task AWideRangeIsAggregatedRatherThanTruncated()
    {
        var (_, token) = await _factory.CreateUserAsync();
        var client = _factory.CreateClientFor(token);

        var symbol = await SeedHourlyCandlesAsync(RangeStart, hours: 48);

        var series = await ReadJsonAsync(
            await client.GetAsync(
                CandlesUrl(symbol, RangeStart, RangeStart.AddHours(47)) + "&maxPoints=6"));

        Assert.Equal(48, series.GetProperty("total").GetInt32());
        Assert.Equal(8, series.GetProperty("bucketSize").GetInt32());
        Assert.Equal(6, series.GetProperty("returned").GetInt32());
        Assert.True(series.GetProperty("isDownsampled").GetBoolean());

        var bars = series.GetProperty("candles");

        // The first bucket covers hours 0 to 7: the first candle's open, the extremes
        // across all eight, the last candle's close, and the summed volume.
        Assert.Equal(RangeStart, bars[0].GetProperty("openTime").GetDateTimeOffset());
        Assert.Equal(100m, bars[0].GetProperty("open").GetDecimal());
        Assert.Equal(108m, bars[0].GetProperty("high").GetDecimal());
        Assert.Equal(99m, bars[0].GetProperty("low").GetDecimal());
        Assert.Equal(107.5m, bars[0].GetProperty("close").GetDecimal());
        Assert.Equal(80m, bars[0].GetProperty("volume").GetDecimal());

        // And the far end is present rather than cut off.
        Assert.Equal(RangeStart.AddHours(40), bars[5].GetProperty("openTime").GetDateTimeOffset());
        Assert.Equal(147.5m, bars[5].GetProperty("close").GetDecimal());
    }

    [Fact]
    public async Task AnEmptyRangeIsAnEmptySeriesRatherThanAnError()
    {
        var (_, token) = await _factory.CreateUserAsync();
        var client = _factory.CreateClientFor(token);

        var series = await ReadJsonAsync(
            await client.GetAsync(CandlesUrl(NewSymbol(), RangeStart, RangeStart.AddHours(47))));

        Assert.Equal(0, series.GetProperty("total").GetInt32());
        Assert.Equal(0, series.GetProperty("candles").GetArrayLength());
    }

    /// GET /gaps and DELETE /candles validate the identical condition. They used to
    /// answer it in two different shapes depending on which verb reached it.
    [Fact]
    public async Task BothVerbsRejectABackwardsRangeTheSameWay()
    {
        var (_, token) = await _factory.CreateUserAsync();
        var client = _factory.CreateClientFor(token);

        var symbol = NewSymbol();
        var backwards = $"source=BinanceFutures&symbol={symbol}&interval=OneHour" +
                        $"&from={Iso(RangeStart.AddHours(47))}&to={Iso(RangeStart)}";

        var fromGaps = await client.GetAsync($"/api/market-data/gaps?{backwards}");
        var fromDelete = await client.DeleteAsync($"/api/market-data/candles?{backwards}");
        var fromSeries = await client.GetAsync($"/api/market-data/candles?{backwards}");

        Assert.Equal(HttpStatusCode.BadRequest, fromGaps.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, fromDelete.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, fromSeries.StatusCode);

        foreach (var response in new[] { fromGaps, fromDelete, fromSeries })
        {
            var problem = await ReadJsonAsync(response);

            Assert.Equal("validation_failed", problem.GetProperty("code").GetString());
            Assert.True(problem.GetProperty("errors").TryGetProperty("from", out _));
        }
    }

    private static string Iso(DateTimeOffset at) =>
        Uri.EscapeDataString(at.ToString("O", CultureInfo.InvariantCulture));

    private static string CandlesUrl(string symbol, DateTimeOffset from, DateTimeOffset to) =>
        $"/api/market-data/candles?source=BinanceFutures&symbol={symbol}" +
        $"&interval=OneHour&from={Iso(from)}&to={Iso(to)}";

    // Guid.CreateVersion7 leads with a timestamp, so two ids minted in the same
    // millisecond share their first hex characters. A symbol cut from the head of one
    // would collide with its neighbour; NewGuid is random the whole way through.
    private static string NewSymbol() =>
        $"MD{Guid.NewGuid():N}"[..12].ToUpperInvariant();

    private static async Task<Guid> QueueBackfillAsync(HttpClient client, string symbol)
    {
        var response = await client.PostAsJsonAsync("/api/market-data/backfill", new
        {
            source = "BinanceFutures",
            symbol,
            interval = "OneHour",
            from = RangeStart.ToString("O", CultureInfo.InvariantCulture),
            to = RangeStart.AddDays(1).ToString("O", CultureInfo.InvariantCulture),
        });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        return (await ReadJsonAsync(response)).GetProperty("id").GetGuid();
    }

    private async Task<string> SeedHourlyCandlesAsync(DateTimeOffset from, int hours)
    {
        var symbol = NewSymbol();

        await using var db = fixture.CreateContext(Guid.Empty);
        var repo = new CandleRepository(db);

        var candles = Enumerable.Range(0, hours)
            .Select(i => Candle.Of(
                CandleSource.BinanceFutures,
                symbol,
                CandleInterval.OneHour,
                from.AddHours(i),
                100m + i,
                101m + i,
                99m + i,
                100.5m + i,
                10m))
            .ToList();

        await repo.UpsertAsync(candles);

        return symbol;
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync(), Json);
}
