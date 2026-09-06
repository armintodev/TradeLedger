using StackExchange.Redis;

namespace TradeLedger.Core.Shared;

public interface IDistributedLock
{
    Task<IAsyncDisposable?> TryAcquireAsync(string key, TimeSpan ttl, CancellationToken ct = default);
}

public sealed class RedisDistributedLock(IConnectionMultiplexer redis) : IDistributedLock
{
    private const string ReleaseScript = """
        if redis.call('GET', KEYS[1]) == ARGV[1] then
            return redis.call('DEL', KEYS[1])
        end
        return 0
        """;

    public async Task<IAsyncDisposable?> TryAcquireAsync(string key, TimeSpan ttl, CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        var redisKey = (RedisKey)$"tl:lock:{key}";
        var token = Guid.NewGuid().ToString("N");

        var acquired = await db.StringSetAsync(redisKey, token, ttl, When.NotExists).ConfigureAwait(false);
        return acquired ? new Handle(db, redisKey, token) : null;
    }

    private sealed class Handle(IDatabase db, RedisKey key, string token) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await db.ScriptEvaluateAsync(ReleaseScript, [key], [token]).ConfigureAwait(false);
            }
            catch (RedisException)
            {
            }
        }
    }
}

public sealed class NoOpDistributedLock : IDistributedLock
{
    public Task<IAsyncDisposable?> TryAcquireAsync(string key, TimeSpan ttl, CancellationToken ct = default) =>
        Task.FromResult<IAsyncDisposable?>(new Handle());

    private sealed class Handle : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
