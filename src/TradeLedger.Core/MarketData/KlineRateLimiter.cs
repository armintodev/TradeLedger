using Microsoft.Extensions.Options;
using StackExchange.Redis;
using TradeLedger.Core.Domain.MarketData;

namespace TradeLedger.Core.MarketData;

public interface IKlineRateLimiter
{
    Task WaitAsync(string egressKey, CancellationToken ct = default);
}

public sealed class RedisKlineRateLimiter(
    IConnectionMultiplexer redis,
    IOptions<MarketDataOptions> options) : IKlineRateLimiter
{
    private readonly int _permitsPerSecond = Math.Max(1, options.Value.RequestsPerSecond);

    private const string Script = """
        local current = redis.call('INCR', KEYS[1])
        if current == 1 then
            redis.call('PEXPIRE', KEYS[1], 1000)
        end
        if current > tonumber(ARGV[1]) then
            return redis.call('PTTL', KEYS[1])
        end
        return 0
        """;

    public async Task WaitAsync(string egressKey, CancellationToken ct = default)
    {
        var db = redis.GetDatabase();

        while (!ct.IsCancellationRequested)
        {
            var key = (RedisKey)$"tl:ratelimit:klines:{egressKey}:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";

            var waitMs = (long)await db.ScriptEvaluateAsync(
                Script,
                [key],
                [_permitsPerSecond]).ConfigureAwait(false);

            if (waitMs <= 0)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(Math.Clamp(waitMs, 10, 1000)), ct).ConfigureAwait(false);
        }

        ct.ThrowIfCancellationRequested();
    }
}

public sealed class NoOpKlineRateLimiter : IKlineRateLimiter
{
    public Task WaitAsync(string egressKey, CancellationToken ct = default) => Task.CompletedTask;
}
