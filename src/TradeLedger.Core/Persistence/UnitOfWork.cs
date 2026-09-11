using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace TradeLedger.Core.Persistence;

public interface IUnitOfWork
{
    bool HasActiveTransaction { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);

    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken ct = default);

    Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> work,
        CancellationToken ct = default);
}

public sealed class UnitOfWork(TradeLedgerDbContext db) : IUnitOfWork
{
    public bool HasActiveTransaction => db.Database.CurrentTransaction is not null;

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);

    public async Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> work,
        CancellationToken ct = default) =>
        await ExecuteInTransactionAsync<object?>(
            async token =>
            {
                await work(token);

                return null;
            },
            ct);

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> work,
        CancellationToken ct = default)
    {
        if (HasActiveTransaction)
        {
            var joined = await work(ct);
            await db.SaveChangesAsync(ct);

            return joined;
        }

        var strategy = db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(
            ct,
            async (_, token) =>
            {
                await using IDbContextTransaction transaction =
                    await db.Database.BeginTransactionAsync(token);

                try
                {
                    var result = await work(token);

                    await db.SaveChangesAsync(token);
                    await transaction.CommitAsync(token);

                    return result;
                }
                catch
                {
                    await transaction.RollbackAsync(CancellationToken.None);

                    throw;
                }
            },
            verifySucceeded: null);
    }
}
