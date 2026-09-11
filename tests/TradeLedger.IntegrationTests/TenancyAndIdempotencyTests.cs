using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TradeLedger.Core.Domain;
using TradeLedger.Core.Integrations.Bitunix;
using TradeLedger.Core.Integrations.Bitunix.Dtos;
using TradeLedger.Core.Persistence.Seed;

namespace TradeLedger.IntegrationTests;

[Collection(nameof(PostgresCollection))]
public sealed class TenancyAndIdempotencyTests(PostgresFixture fixture)
{
    [Fact]
    public async Task ApplyingTheSamePositionTwiceWritesOneRow()
    {
        var (userId, accountId) = await SeedUserAndAccountAsync();

        await UpsertPositionAsync(userId, accountId, SamplePosition("pos-idem-1"));
        await UpsertPositionAsync(userId, accountId, SamplePosition("pos-idem-1"));

        await using var db = fixture.CreateContext(userId);
        var count = await db.Trades.CountAsync(t => t.ExchangePositionId == "pos-idem-1");

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task DatabaseRejectsDuplicateExchangePositionIdForSameAccount()
    {
        var (userId, accountId) = await SeedUserAndAccountAsync();

        await using var db = fixture.CreateContext(userId);

        db.Trades.Add(NewTrade(userId, accountId, "pos-dup"));
        await db.SaveChangesAsync();

        db.Trades.Add(NewTrade(userId, accountId, "pos-dup"));

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task SamePositionIdIsAllowedAcrossDifferentAccounts()
    {
        var (userId, accountA) = await SeedUserAndAccountAsync();
        var accountB = await AddAccountAsync(userId, "Bitunix Spot");

        await using var db = fixture.CreateContext(userId);

        db.Trades.Add(NewTrade(userId, accountA, "pos-shared"));
        db.Trades.Add(NewTrade(userId, accountB, "pos-shared"));

        await db.SaveChangesAsync();

        Assert.Equal(2, await db.Trades.CountAsync(t => t.ExchangePositionId == "pos-shared"));
    }

    [Fact]
    public async Task GlobalQueryFilterHidesAnotherUsersTrades()
    {
        var (userA, accountA) = await SeedUserAndAccountAsync();
        var (userB, accountB) = await SeedUserAndAccountAsync();

        await using (var db = fixture.CreateContext(userA))
        {
            db.Trades.Add(NewTrade(userA, accountA, "pos-tenant-a"));
            await db.SaveChangesAsync();
        }

        await using (var db = fixture.CreateContext(userB))
        {
            db.Trades.Add(NewTrade(userB, accountB, "pos-tenant-b"));
            await db.SaveChangesAsync();
        }

        await using var asUserA = fixture.CreateContext(userA);
        var visible = await asUserA.Trades.Select(t => t.ExchangePositionId).ToListAsync();

        Assert.Contains("pos-tenant-a", visible);
        Assert.DoesNotContain("pos-tenant-b", visible);
    }

    [Fact]
    public async Task SaveChangesStampsUserIdAutomatically()
    {
        var (userId, accountId) = await SeedUserAndAccountAsync();

        await using var db = fixture.CreateContext(userId);

        var trade = Trade.OpenManual(new NewManualTrade
        {
            AccountId = accountId,
            Symbol = "ETHUSDT",
            Side = TradeSide.Long,
            EntryPrice = 3000m,
            Quantity = 1m,
            OpenedAt = DateTimeOffset.UtcNow,
        });

        db.Trades.Add(trade);
        await db.SaveChangesAsync();

        Assert.Equal(userId, trade.UserId);
    }

    [Fact]
    public async Task DecimalPrecisionSurvivesRoundTrip()
    {
        var (userId, accountId) = await SeedUserAndAccountAsync();

        const decimal tinyPrice = 0.000000012345678901m;
        const decimal preciseQty = 123456.123456789012m;

        Guid id;

        await using (var db = fixture.CreateContext(userId))
        {
            var trade = Trade.OpenManual(new NewManualTrade
            {
                AccountId = accountId,
                Symbol = "PEPEUSDT",
                Side = TradeSide.Long,
                EntryPrice = tinyPrice,
                Quantity = preciseQty,
                OpenedAt = DateTimeOffset.UtcNow,
            });

            id = trade.Id;

            db.Trades.Add(trade);

            await db.SaveChangesAsync();
        }

        await using var read = fixture.CreateContext(userId);
        var loaded = await read.Trades.FirstAsync(t => t.Id == id);

        Assert.Equal(tinyPrice, loaded.EntryPrice);
        Assert.Equal(preciseQty, loaded.Quantity);
    }

    [Fact]
    public async Task TaxonomySeederIsIdempotent()
    {
        var userId = await AddUserAsync();

        await using var db = fixture.CreateContext(userId);

        await TaxonomySeeder.SeedAsync(db, userId);
        var afterFirst = await db.TaxonomyTerms.CountAsync();

        await TaxonomySeeder.SeedAsync(db, userId);
        var afterSecond = await db.TaxonomyTerms.CountAsync();

        Assert.Equal(afterFirst, afterSecond);
        Assert.True(afterFirst > 0);
    }

    [Fact]
    public async Task TaxonomySeederPreservesOwnerSpellingsVerbatim()
    {
        var userId = await AddUserAsync();

        await using var db = fixture.CreateContext(userId);
        await TaxonomySeeder.SeedAsync(db, userId);

        var mentalStates = await db.TaxonomyTerms
            .Where(t => t.Kind == TaxonomyKind.MentalState)
            .Select(t => t.Name)
            .ToListAsync();

        Assert.Contains("Stressfull", mentalStates);
        Assert.Contains("boredo", mentalStates);
    }

    private async Task UpsertPositionAsync(Guid userId, Guid accountId, HistoryPositionDto dto)
    {
        await using var db = fixture.CreateContext(userId);

        var trade = await db.Trades
            .FirstOrDefaultAsync(t => t.AccountId == accountId
                                      && t.ExchangePositionId == dto.PositionId);

        var snapshot = BitunixPositionMapper.ToSnapshot(dto);

        if (trade is null)
        {
            trade = Trade.FromExchange(userId, accountId, snapshot);
            db.Trades.Add(trade);
        }
        else
        {
            trade.ApplyExchangeSnapshot(snapshot);
        }

        await db.SaveChangesAsync();
    }

    private async Task<(Guid UserId, Guid AccountId)> SeedUserAndAccountAsync()
    {
        var userId = await AddUserAsync();
        var accountId = await AddAccountAsync(userId, "Bitunix Futures");
        return (userId, accountId);
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

    private async Task<Guid> AddAccountAsync(Guid userId, string name)
    {
        await using var db = fixture.CreateContext(userId);

        var account = Account.Create(
            name,
            AccountKind.ExchangeFutures,
            Venue.Bitunix,
            SyncMode.Api,
            userId: userId);

        db.Accounts.Add(account);
        await db.SaveChangesAsync();

        return account.Id;
    }

    private static Trade NewTrade(Guid userId, Guid accountId, string positionId) =>
        Trade.FromExchange(
            userId,
            accountId,
            new ExchangePositionSnapshot
            {
                ExchangePositionId = positionId,
                Symbol = "BTCUSDT",
                Side = TradeSide.Long,
                EntryPrice = 60000m,
                Quantity = 0.01m,
                OpenedAt = DateTimeOffset.UtcNow,
                StillOpen = true,
            });

    private static HistoryPositionDto SamplePosition(string positionId) => new()
    {
        PositionId = positionId,
        Symbol = "BTCUSDT",
        MaxQty = 0.005m,
        EntryPrice = 64250.5m,
        ClosePrice = 65100.25m,
        Side = "LONG",
        MarginMode = "ISOLATION",
        Leverage = 10,
        Fee = 0.42m,
        Funding = -0.08m,
        RealizedPnl = 4.25m,
        Ctime = 1735689600000,
        Mtime = 1735693200000,
    };
}
