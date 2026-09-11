using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TradeLedger.Core.Domain;
using TradeLedger.Core.Domain.MarketData;
using TradeLedger.Core.MarketData;

namespace TradeLedger.IntegrationTests;

[Collection(nameof(PostgresCollection))]
public sealed class CandleRepositoryTests(PostgresFixture fixture)
{
    [Fact]
    public async Task WritingTheSameRangeTwiceInsertsOnce()
    {
        var symbol = NewSymbol();

        await using var db = fixture.CreateContext(Guid.Empty);
        var repo = new CandleRepository(db);

        var batch = Series(symbol, CandleInterval.OneHour, At("2025-01-01T00:00:00Z"), 24);

        var firstWrite = await repo.UpsertAsync(batch);
        var secondWrite = await repo.UpsertAsync(
            Series(symbol, CandleInterval.OneHour, At("2025-01-01T00:00:00Z"), 24));

        Assert.Equal(24, firstWrite);
        Assert.Equal(0, secondWrite);

        var stored = await db.Candles.CountAsync(c => c.Symbol == symbol);
        Assert.Equal(24, stored);
    }

    [Fact]
    public async Task AnOverlappingRangeOnlyInsertsTheNewCandles()
    {
        var symbol = NewSymbol();

        await using var db = fixture.CreateContext(Guid.Empty);
        var repo = new CandleRepository(db);

        await repo.UpsertAsync(Series(symbol, CandleInterval.OneHour, At("2025-01-01T00:00:00Z"), 12));

        var overlapping = await repo.UpsertAsync(
            Series(symbol, CandleInterval.OneHour, At("2025-01-01T06:00:00Z"), 12));

        Assert.Equal(6, overlapping);
        Assert.Equal(18, await db.Candles.CountAsync(c => c.Symbol == symbol));
    }

    [Fact]
    public async Task DecimalPrecisionSurvivesTheRoundTrip()
    {
        var symbol = NewSymbol();

        await using var db = fixture.CreateContext(Guid.Empty);
        var repo = new CandleRepository(db);

        const decimal tiny = 0.000000012345678901m;

        await repo.UpsertAsync(
        [
            Candle.Of(
                CandleSource.BinanceFutures,
                symbol,
                CandleInterval.OneHour,
                At("2025-01-01T00:00:00Z"),
                open: tiny,
                high: tiny,
                low: tiny,
                close: tiny,
                volume: 123456.123456789012m),
        ]);

        var loaded = await db.Candles.AsNoTracking().FirstAsync(c => c.Symbol == symbol);

        Assert.Equal(tiny, loaded.Open);
        Assert.Equal(123456.123456789012m, loaded.Volume);
    }

    [Fact]
    public async Task ACompleteRangeReportsNoGaps()
    {
        var symbol = NewSymbol();

        await using var db = fixture.CreateContext(Guid.Empty);
        var repo = new CandleRepository(db);

        await repo.UpsertAsync(Series(symbol, CandleInterval.OneHour, At("2025-01-01T00:00:00Z"), 24));

        var gaps = await repo.FindGapsAsync(
            CandleSource.BinanceFutures, symbol, CandleInterval.OneHour,
            At("2025-01-01T00:00:00Z"), At("2025-01-01T23:00:00Z"));

        Assert.Empty(gaps);
    }

    [Fact]
    public async Task AHoleInTheMiddleIsReportedAsOneGap()
    {
        var symbol = NewSymbol();

        await using var db = fixture.CreateContext(Guid.Empty);
        var repo = new CandleRepository(db);

        var series = Series(symbol, CandleInterval.OneHour, At("2025-01-01T00:00:00Z"), 24)
            .Where(c => c.OpenTime < At("2025-01-01T10:00:00Z")
                        || c.OpenTime > At("2025-01-01T12:00:00Z"))
            .ToList();

        await repo.UpsertAsync(series);

        var gaps = await repo.FindGapsAsync(
            CandleSource.BinanceFutures, symbol, CandleInterval.OneHour,
            At("2025-01-01T00:00:00Z"), At("2025-01-01T23:00:00Z"));

        var gap = Assert.Single(gaps);
        Assert.Equal(At("2025-01-01T10:00:00Z"), gap.From);
        Assert.Equal(At("2025-01-01T12:00:00Z"), gap.To);
        Assert.Equal(3, gap.MissingCount);
    }

    [Fact]
    public async Task SeveralHolesAreReportedSeparately()
    {
        var symbol = NewSymbol();

        await using var db = fixture.CreateContext(Guid.Empty);
        var repo = new CandleRepository(db);

        var series = Series(symbol, CandleInterval.OneHour, At("2025-01-01T00:00:00Z"), 24)
            .Where(c => c.OpenTime != At("2025-01-01T03:00:00Z")
                        && c.OpenTime != At("2025-01-01T17:00:00Z"))
            .ToList();

        await repo.UpsertAsync(series);

        var gaps = await repo.FindGapsAsync(
            CandleSource.BinanceFutures, symbol, CandleInterval.OneHour,
            At("2025-01-01T00:00:00Z"), At("2025-01-01T23:00:00Z"));

        Assert.Equal(2, gaps.Count);
        Assert.All(gaps, g => Assert.Equal(1, g.MissingCount));
    }

    [Fact]
    public async Task AnEmptyRangeIsOneGapCoveringTheWholeSpan()
    {
        var symbol = NewSymbol();

        await using var db = fixture.CreateContext(Guid.Empty);
        var repo = new CandleRepository(db);

        var gaps = await repo.FindGapsAsync(
            CandleSource.BinanceFutures, symbol, CandleInterval.OneHour,
            At("2025-01-01T00:00:00Z"), At("2025-01-01T05:00:00Z"));

        var gap = Assert.Single(gaps);
        Assert.Equal(6, gap.MissingCount);
    }

    [Fact]
    public async Task MissingCandlesAtTheEdgesAreStillGaps()
    {
        var symbol = NewSymbol();

        await using var db = fixture.CreateContext(Guid.Empty);
        var repo = new CandleRepository(db);

        await repo.UpsertAsync(Series(symbol, CandleInterval.OneHour, At("2025-01-01T02:00:00Z"), 3));

        var gaps = await repo.FindGapsAsync(
            CandleSource.BinanceFutures, symbol, CandleInterval.OneHour,
            At("2025-01-01T00:00:00Z"), At("2025-01-01T06:00:00Z"));

        Assert.Equal(2, gaps.Count);
        Assert.Equal(At("2025-01-01T00:00:00Z"), gaps[0].From);
        Assert.Equal(At("2025-01-01T06:00:00Z"), gaps[^1].To);
    }

    [Fact]
    public async Task IntervalsDoNotContaminateEachOther()
    {
        var symbol = NewSymbol();

        await using var db = fixture.CreateContext(Guid.Empty);
        var repo = new CandleRepository(db);

        await repo.UpsertAsync(Series(symbol, CandleInterval.OneHour, At("2025-01-01T00:00:00Z"), 24));

        var gaps = await repo.FindGapsAsync(
            CandleSource.BinanceFutures, symbol, CandleInterval.FifteenMinutes,
            At("2025-01-01T00:00:00Z"), At("2025-01-01T01:00:00Z"));

        Assert.NotEmpty(gaps);
    }

    [Fact]
    public async Task SourcesDoNotContaminateEachOther()
    {
        var symbol = NewSymbol();

        await using var db = fixture.CreateContext(Guid.Empty);
        var repo = new CandleRepository(db);

        await repo.UpsertAsync(Series(symbol, CandleInterval.OneHour, At("2025-01-01T00:00:00Z"), 5));

        var csv = Series(
            symbol,
            CandleInterval.OneHour,
            At("2025-01-01T00:00:00Z"),
            5,
            CandleSource.CsvImport);

        var inserted = await repo.UpsertAsync(csv);

        Assert.Equal(5, inserted);
        Assert.Equal(10, await db.Candles.CountAsync(c => c.Symbol == symbol));
    }

    [Fact]
    public async Task CoverageReportsWhatIsStored()
    {
        var symbol = NewSymbol();

        await using var db = fixture.CreateContext(Guid.Empty);
        var repo = new CandleRepository(db);

        await repo.UpsertAsync(Series(symbol, CandleInterval.FourHours, At("2025-01-01T00:00:00Z"), 10));

        var coverage = await repo.GetCoverageAsync();
        var row = Assert.Single(coverage, c => c.Symbol == symbol);

        Assert.Equal(10, row.RowCount);
        Assert.Equal(At("2025-01-01T00:00:00Z"), row.FirstOpenTime);
        Assert.Equal(At("2025-01-02T12:00:00Z"), row.LastOpenTime);
    }

    [Fact]
    public async Task DeletingARangeRemovesOnlyThatRange()
    {
        var symbol = NewSymbol();

        await using var db = fixture.CreateContext(Guid.Empty);
        var repo = new CandleRepository(db);

        await repo.UpsertAsync(Series(symbol, CandleInterval.OneHour, At("2025-01-01T00:00:00Z"), 24));

        var deleted = await repo.DeleteRangeAsync(
            CandleSource.BinanceFutures, symbol, CandleInterval.OneHour,
            At("2025-01-01T06:00:00Z"), At("2025-01-01T11:00:00Z"));

        Assert.Equal(6, deleted);
        Assert.Equal(18, await db.Candles.CountAsync(c => c.Symbol == symbol));
    }

    [Fact]
    public async Task SymbolAliasesResolvePerSourceAndFallBackToTheCanonicalName()
    {
        await using var db = fixture.CreateContext(Guid.Empty);
        var repo = new CandleRepository(db);

        var canonical = NewSymbol();

        db.MarketSymbolAliases.Add(MarketSymbolAlias.Of(
            canonical,
            CandleSource.BinanceFutures,
            "MAPPEDUSDT"));

        await db.SaveChangesAsync();

        Assert.Equal(
            "MAPPEDUSDT",
            await repo.ResolveSymbolAsync(canonical, CandleSource.BinanceFutures));

        Assert.Equal(
            canonical,
            await repo.ResolveSymbolAsync(canonical, CandleSource.BinanceSpot));
    }

    [Fact]
    public async Task CandlesAreSharedReferenceDataVisibleToEveryUser()
    {
        var symbol = NewSymbol();
        var userA = await AddUserAsync();
        var userB = await AddUserAsync();

        await using (var db = fixture.CreateContext(userA))
        {
            await new CandleRepository(db).UpsertAsync(
                Series(symbol, CandleInterval.OneHour, At("2025-01-01T00:00:00Z"), 4));
        }

        await using var asUserB = fixture.CreateContext(userB);

        Assert.Equal(4, await asUserB.Candles.CountAsync(c => c.Symbol == symbol));
    }

    [Fact]
    public async Task CandleImportAuditRowsStayPrivateToTheUserWhoUploadedThem()
    {
        var userA = await AddUserAsync();
        var userB = await AddUserAsync();
        var symbol = NewSymbol();

        await using (var db = fixture.CreateContext(userA))
        {
            db.CandleImports.Add(CandleImport.Record(new NewCandleImport
            {
                UserId = userA,
                FileName = "mine.csv",
                FileBytes = 10,
                ContentSha256 = new string('a', 64),
                Source = CandleSource.CsvImport,
                Symbol = symbol,
                Interval = CandleInterval.OneHour,
            }));

            await db.SaveChangesAsync();
        }

        await using var asUserB = fixture.CreateContext(userB);

        Assert.False(await asUserB.CandleImports.AnyAsync(i => i.Symbol == symbol));
    }

    private static List<Candle> Series(
        string symbol,
        CandleInterval interval,
        DateTimeOffset start,
        int count,
        CandleSource source = CandleSource.BinanceFutures)
    {
        var candles = new List<Candle>(count);
        var duration = interval.Duration();

        for (var i = 0; i < count; i++)
        {
            var openTime = start + duration * i;

            candles.Add(Candle.Of(
                source,
                symbol,
                interval,
                openTime,
                100m + i,
                110m + i,
                95m + i,
                105m + i,
                volume: 1000m + i));
        }

        return candles;
    }

    private async Task<Guid> AddUserAsync()
    {
        var userId = Guid.CreateVersion7();
        var email = $"{userId:N}@example.test";

        await using var db = fixture.CreateContext(userId);

        db.Users.Add(new AppUser
        {
            Id = userId,
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            SecurityStamp = Guid.NewGuid().ToString(),
            PasswordHash = new PasswordHasher<AppUser>().HashPassword(null!, "not-used-in-tests"),
        });

        await db.SaveChangesAsync();
        return userId;
    }

    private static string NewSymbol() =>
        "T" + Guid.CreateVersion7().ToString("N")[..12].ToUpperInvariant();

    private static DateTimeOffset At(string iso) =>
        DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture);
}
