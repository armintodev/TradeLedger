using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TradeLedger.Core.Domain;
using TradeLedger.Core.Persistence;

namespace TradeLedger.IntegrationTests;

[Collection(nameof(PostgresCollection))]
public sealed class UnitOfWorkTests(PostgresFixture fixture)
{
    [Fact]
    public async Task WorkThatCompletesIsCommitted()
    {
        var (userId, accountId) = await SeedAsync();

        await using var db = fixture.CreateContext(userId);
        var unitOfWork = new UnitOfWork(db);

        await unitOfWork.ExecuteInTransactionAsync(
            _ =>
            {
                db.Trades.Add(NewTrade(accountId, "COMMITTED"));

                return Task.CompletedTask;
            });

        await using var read = fixture.CreateContext(userId);

        Assert.True(await read.Trades.AnyAsync(t => t.Symbol == "COMMITTED"));
    }

    [Fact]
    public async Task EverySaveInsideTheTransactionIsRolledBackTogether()
    {
        var (userId, accountId) = await SeedAsync();

        await using var db = fixture.CreateContext(userId);
        var unitOfWork = new UnitOfWork(db);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => unitOfWork.ExecuteInTransactionAsync(async token =>
            {
                db.Trades.Add(NewTrade(accountId, "FIRSTWRITE"));
                await db.SaveChangesAsync(token);

                db.Trades.Add(NewTrade(accountId, "SECONDWRITE"));
                await db.SaveChangesAsync(token);

                throw new InvalidOperationException("the third step failed");
            }));

        Assert.Equal("the third step failed", thrown.Message);

        await using var read = fixture.CreateContext(userId);

        Assert.False(await read.Trades.AnyAsync(t => t.Symbol == "FIRSTWRITE"));
        Assert.False(await read.Trades.AnyAsync(t => t.Symbol == "SECONDWRITE"));
    }

    [Fact]
    public async Task AConstraintViolationRollsBackTheWholeUnit()
    {
        var (userId, accountId) = await SeedAsync();

        await using var db = fixture.CreateContext(userId);
        var unitOfWork = new UnitOfWork(db);

        await Assert.ThrowsAsync<DbUpdateException>(
            () => unitOfWork.ExecuteInTransactionAsync(async token =>
            {
                db.Trades.Add(NewTrade(accountId, "GOODROW"));
                await db.SaveChangesAsync(token);

                db.Trades.Add(NewTrade(Guid.CreateVersion7(), "ORPHANROW"));
            }));

        await using var read = fixture.CreateContext(userId);

        Assert.False(await read.Trades.AnyAsync(t => t.Symbol == "GOODROW"));
    }

    [Fact]
    public async Task NestedWorkJoinsTheOuterTransactionRatherThanOpeningASecond()
    {
        var (userId, accountId) = await SeedAsync();

        await using var db = fixture.CreateContext(userId);
        var unitOfWork = new UnitOfWork(db);

        Assert.False(unitOfWork.HasActiveTransaction);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => unitOfWork.ExecuteInTransactionAsync(async outer =>
            {
                Assert.True(unitOfWork.HasActiveTransaction);

                db.Trades.Add(NewTrade(accountId, "OUTERROW"));

                await unitOfWork.ExecuteInTransactionAsync(
                    inner =>
                    {
                        db.Trades.Add(NewTrade(accountId, "INNERROW"));

                        return Task.CompletedTask;
                    },
                    outer);

                throw new InvalidOperationException("outer failed after the inner work");
            }));

        await using var read = fixture.CreateContext(userId);

        Assert.False(await read.Trades.AnyAsync(t => t.Symbol == "OUTERROW"));
        Assert.False(await read.Trades.AnyAsync(t => t.Symbol == "INNERROW"));
    }

    [Fact]
    public async Task TheTransactionIsClosedOnceTheWorkReturns()
    {
        var (userId, accountId) = await SeedAsync();

        await using var db = fixture.CreateContext(userId);
        var unitOfWork = new UnitOfWork(db);

        await unitOfWork.ExecuteInTransactionAsync(
            _ =>
            {
                db.Trades.Add(NewTrade(accountId, "CLOSEDAFTER"));

                return Task.CompletedTask;
            });

        Assert.False(unitOfWork.HasActiveTransaction);
    }

    private static Trade NewTrade(Guid accountId, string symbol) => Trade.OpenManual(new NewManualTrade
    {
        AccountId = accountId,
        Symbol = symbol,
        Side = TradeSide.Long,
        EntryPrice = 100m,
        Quantity = 1m,
        OpenedAt = DateTimeOffset.UtcNow,
    });

    private async Task<(Guid UserId, Guid AccountId)> SeedAsync()
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

        var account = Account.Create(
            $"Account {Guid.CreateVersion7():N}"[..24],
            AccountKind.ExchangeFutures,
            Venue.Bitunix,
            SyncMode.Api,
            userId: userId);

        db.Accounts.Add(account);
        await db.SaveChangesAsync();

        return (userId, account.Id);
    }
}
