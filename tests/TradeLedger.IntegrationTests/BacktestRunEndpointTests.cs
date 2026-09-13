using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TradeLedger.Core.Domain;
using TradeLedger.Core.Domain.Backtesting;
using TradeLedger.Core.Domain.MarketData;
using TradeLedger.Core.MarketData;

namespace TradeLedger.IntegrationTests;

[Collection(nameof(PostgresCollection))]
public sealed class BacktestRunEndpointTests(PostgresFixture fixture) : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static readonly DateTimeOffset RangeStart = new(2025, 3, 1, 0, 0, 0, TimeSpan.Zero);

    private ApiFactory _factory = null!;

    public Task InitializeAsync()
    {
        _factory = new ApiFactory(fixture.ConnectionString);

        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    /// The two halves of what DataQuality is supposed to mean. It used to be assigned
    /// from the allowGaps flag before a single candle had been looked at, so a run over
    /// complete data was stamped Gapped purely for having asked permission.
    [Fact]
    public async Task AllowingGapsOverCompleteDataStillLeavesTheRunClean()
    {
        var (_, token) = await _factory.CreateUserAsync();
        var client = _factory.CreateClientFor(token);

        var symbol = await SeedHourlyCandlesAsync(RangeStart, hours: 48);
        var accountId = await CreateAccountAsync(client);

        var run = await QueueAsync(
            client, accountId, symbol, RangeStart, RangeStart.AddHours(47), allowGaps: true);

        Assert.Equal("Clean", run.GetProperty("dataQuality").GetString());
        Assert.True(run.GetProperty("allowGaps").GetBoolean());
    }

    [Fact]
    public async Task AllowingGapsOverARealHoleStampsTheRunGapped()
    {
        var (_, token) = await _factory.CreateUserAsync();
        var client = _factory.CreateClientFor(token);

        var symbol = await SeedHourlyCandlesAsync(RangeStart, hours: 48);
        await DeleteCandleAsync(symbol, RangeStart.AddHours(20));

        var accountId = await CreateAccountAsync(client);

        var run = await QueueAsync(
            client, accountId, symbol, RangeStart, RangeStart.AddHours(47), allowGaps: true);

        Assert.Equal("Gapped", run.GetProperty("dataQuality").GetString());
        Assert.True(run.GetProperty("allowGaps").GetBoolean());
    }

    [Fact]
    public async Task AHoleWithoutPermissionIsStillRefused()
    {
        var (_, token) = await _factory.CreateUserAsync();
        var client = _factory.CreateClientFor(token);

        var symbol = await SeedHourlyCandlesAsync(RangeStart, hours: 48);
        await DeleteCandleAsync(symbol, RangeStart.AddHours(20));

        var accountId = await CreateAccountAsync(client);

        var response = await client.PostAsJsonAsync("/api/backtests", Request(
            accountId, symbol, RangeStart, RangeStart.AddHours(47), allowGaps: false));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        var problem = await ReadJsonAsync(response);
        Assert.Equal("candle_data_has_gaps", problem.GetProperty("code").GetString());
    }

    /// Every other 404 in the feature is a ProblemDetails; this one used to be a
    /// bodyless Results.NotFound(), which a client can only render as a blank.
    [Fact]
    public async Task AMissingRunComesBackAsAProblemDocument()
    {
        var (_, token) = await _factory.CreateUserAsync();
        var client = _factory.CreateClientFor(token);

        var response = await client.GetAsync($"/api/backtests/{Guid.CreateVersion7()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var problem = await ReadJsonAsync(response);

        Assert.Equal("not_found", problem.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("traceId").GetString()));
    }

    /// A run keeps its own copy of the rule it executed so an old result stays
    /// explainable after the strategy moves on. That copy is only reachable if the
    /// by-id fetch returns it, and a list of runs should not carry fifty rule trees.
    [Fact]
    public async Task TheFrozenRuleTravelsWithTheRunButNotWithTheList()
    {
        var (_, token) = await _factory.CreateUserAsync();
        var client = _factory.CreateClientFor(token);

        var symbol = await SeedHourlyCandlesAsync(RangeStart, hours: 48);
        var accountId = await CreateAccountAsync(client);
        var strategyId = await CreateStrategyAsync(client);

        var queued = await QueueAsync(
            client, accountId, symbol, RangeStart, RangeStart.AddHours(47),
            allowGaps: false, strategyId: strategyId);

        var runId = queued.GetProperty("id").GetGuid();

        var fetched = await ReadJsonAsync(await client.GetAsync($"/api/backtests/{runId}"));

        Assert.Equal(JsonValueKind.Object, fetched.GetProperty("rule").ValueKind);

        Assert.Equal(
            "Ema",
            fetched.GetProperty("rule").GetProperty("indicators")[0].GetProperty("type").GetString());

        var listed = await ReadJsonAsync(
            await client.GetAsync($"/api/backtests?accountId={accountId}"));

        var row = listed.GetProperty("items")[0];

        Assert.Equal(runId, row.GetProperty("id").GetGuid());
        Assert.Equal(JsonValueKind.Null, row.GetProperty("rule").ValueKind);
        Assert.False(string.IsNullOrWhiteSpace(row.GetProperty("ruleHash").GetString()));
    }

    /// The runs list one file over is paged with a total; the trades were a bare
    /// array, so no page count could be derived from them.
    [Fact]
    public async Task TheTradesOfARunArePagedWithATotal()
    {
        var (userId, token) = await _factory.CreateUserAsync();
        var client = _factory.CreateClientFor(token);

        var symbol = await SeedHourlyCandlesAsync(RangeStart, hours: 48);
        var accountId = await CreateAccountAsync(client);

        var queued = await QueueAsync(
            client, accountId, symbol, RangeStart, RangeStart.AddHours(47), allowGaps: true);

        var runId = queued.GetProperty("id").GetGuid();

        await SeedTradesAsync(userId, runId, symbol, count: 5);

        var page = await ReadJsonAsync(
            await client.GetAsync($"/api/backtests/{runId}/trades?page=1&pageSize=2"));

        Assert.Equal(5, page.GetProperty("total").GetInt32());
        Assert.Equal(3, page.GetProperty("totalPages").GetInt32());
        Assert.True(page.GetProperty("hasMore").GetBoolean());
        Assert.Equal(2, page.GetProperty("items").GetArrayLength());

        var last = await ReadJsonAsync(
            await client.GetAsync($"/api/backtests/{runId}/trades?page=3&pageSize=2"));

        Assert.False(last.GetProperty("hasMore").GetBoolean());
        Assert.Equal(1, last.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task TheFillsBehindASimulatedTradeAreReachable()
    {
        var (userId, token) = await _factory.CreateUserAsync();
        var client = _factory.CreateClientFor(token);

        var symbol = await SeedHourlyCandlesAsync(RangeStart, hours: 48);
        var accountId = await CreateAccountAsync(client);

        var queued = await QueueAsync(
            client, accountId, symbol, RangeStart, RangeStart.AddHours(47), allowGaps: true);

        var runId = queued.GetProperty("id").GetGuid();

        await SeedTradesAsync(userId, runId, symbol, count: 1);

        var trades = await ReadJsonAsync(await client.GetAsync($"/api/backtests/{runId}/trades"));
        var tradeId = trades.GetProperty("items")[0].GetProperty("id").GetGuid();

        var executions = await ReadJsonAsync(
            await client.GetAsync($"/api/backtests/{runId}/trades/{tradeId}/executions"));

        Assert.Equal(2, executions.GetArrayLength());
        Assert.Equal("Open", executions[0].GetProperty("role").GetString());
        Assert.Equal("Close", executions[1].GetProperty("role").GetString());
        Assert.Equal(tradeId, executions[0].GetProperty("backtestTradeId").GetGuid());

        var missing = await client.GetAsync(
            $"/api/backtests/{runId}/trades/{Guid.CreateVersion7()}/executions");

        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    // Guid.CreateVersion7 leads with a timestamp, so two ids minted in the same
    // millisecond share their first hex characters. A symbol cut from the head of one
    // would collide with its neighbour; NewGuid is random the whole way through.
    private static string NewSymbol() =>
        $"BT{Guid.NewGuid():N}"[..12].ToUpperInvariant();

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

    private async Task DeleteCandleAsync(string symbol, DateTimeOffset at)
    {
        await using var db = fixture.CreateContext(Guid.Empty);

        await db.Candles
            .Where(c => c.Symbol == symbol
                        && c.Interval == CandleInterval.OneHour
                        && c.OpenTime == at)
            .ExecuteDeleteAsync();
    }

    private async Task SeedTradesAsync(Guid userId, Guid runId, string symbol, int count)
    {
        await using var db = fixture.CreateContext(userId);

        for (var i = 1; i <= count; i++)
        {
            var trade = BacktestTrade.Close(new ClosedBacktestPosition
            {
                Sequence = i,
                Symbol = symbol,
                Side = TradeSide.Long,
                OpenedAt = RangeStart.AddHours(i),
                ClosedAt = RangeStart.AddHours(i + 1),
                EntryBarIndex = i,
                ExitBarIndex = i + 1,
                EntryPrice = 100m,
                ExitPrice = 104m,
                Quantity = 1m,
                Leverage = 5,
                PositionMargin = 20m,
                OrderValue = 100m,
                StopLossPrice = 98m,
                TakeProfitPrice = 104m,
                LiquidationPrice = 80m,
                GrossProfitLoss = 4m,
                Fees = 0.1m,
                Funding = 0m,
                RiskAmount = 2m,
                MaxAdverse = 1m,
                MaxFavourable = 4m,
                BalanceAfter = 10_000m + (i * 4m),
                PlannedReturnR = 2m,
                ExitReason = BacktestExitReason.TakeProfit,
                IntrabarResolution = IntrabarResolution.Unambiguous,
            });

            trade.RecordExecution(ExecutionRole.Open, 100m, 1m, 0.05m, RangeStart.AddHours(i), i);

            trade.RecordExecution(
                ExecutionRole.Close, 104m, 1m, 0.05m, RangeStart.AddHours(i + 1), i + 1);

            trade.BelongsTo(userId, runId);

            db.BacktestTrades.Add(trade);
        }

        await db.SaveChangesAsync();
    }

    private static async Task<Guid> CreateAccountAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/backtests/accounts", new
        {
            name = $"Account {Guid.CreateVersion7():N}"[..20],
            startingBalance = 10_000m,
            mode = "Independent",
        });

        response.EnsureSuccessStatusCode();

        return (await ReadJsonAsync(response)).GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateStrategyAsync(HttpClient client)
    {
        const string rule = """
            {
              "version": 1,
              "indicators": [
                { "id": "emaFast", "type": "Ema", "source": "Close", "params": { "period": 9 } }
              ],
              "entry": {
                "long": {
                  "op": "GreaterThan",
                  "left": { "ref": "emaFast" },
                  "right": { "const": 1 }
                }
              },
              "stopLoss": { "kind": "Percent", "percent": 1.5 }
            }
            """;

        var response = await client.PostAsJsonAsync("/api/backtests/strategies", new
        {
            name = $"Strategy {Guid.CreateVersion7():N}"[..20],
            rule = JsonSerializer.Deserialize<JsonElement>(rule),
        });

        response.EnsureSuccessStatusCode();

        return (await ReadJsonAsync(response)).GetProperty("id").GetGuid();
    }

    private static object Request(
        Guid accountId,
        string symbol,
        DateTimeOffset from,
        DateTimeOffset to,
        bool allowGaps,
        Guid? strategyId = null) => new
        {
            backtestAccountId = accountId,
            backtestStrategyId = strategyId,
            from = from.ToString("O", CultureInfo.InvariantCulture),
            to = to.ToString("O", CultureInfo.InvariantCulture),
            symbol,
            source = "BinanceFutures",
            interval = "OneHour",
            allowGaps,
        };

    private static async Task<JsonElement> QueueAsync(
        HttpClient client,
        Guid accountId,
        string symbol,
        DateTimeOffset from,
        DateTimeOffset to,
        bool allowGaps,
        Guid? strategyId = null)
    {
        var response = await client.PostAsJsonAsync(
            "/api/backtests", Request(accountId, symbol, from, to, allowGaps, strategyId));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        return await ReadJsonAsync(response);
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync(), Json);
}
