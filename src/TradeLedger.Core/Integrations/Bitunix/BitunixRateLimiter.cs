using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace TradeLedger.Core.Integrations.Bitunix;

public interface IBitunixRateLimiter
{
    Task WaitAsync(string credentialKey, CancellationToken ct = default);
}

public sealed class RedisBitunixRateLimiter(
    IConnectionMultiplexer redis,
    IOptions<BitunixOptions> options) : IBitunixRateLimiter
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

    public async Task WaitAsync(string credentialKey, CancellationToken ct = default)
    {
        var db = redis.GetDatabase();

        while (!ct.IsCancellationRequested)
        {
            var key = (RedisKey)$"tl:ratelimit:bitunix:{credentialKey}:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";

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

public sealed class NoOpBitunixRateLimiter : IBitunixRateLimiter
{
    public Task WaitAsync(string credentialKey, CancellationToken ct = default) => Task.CompletedTask;
}
