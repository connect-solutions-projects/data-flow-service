using System;
using System.Globalization;
using System.Threading.Tasks;
using DataFlow.Api.Services.Interfaces;
using StackExchange.Redis;

namespace DataFlow.Api.Services;

public class RedisRateLimiter : IRedisRateLimiter
{
    private readonly IConnectionMultiplexer _mux;

    public RedisRateLimiter(IConnectionMultiplexer mux)
    {
        _mux = mux;
    }

    public async Task<RateLimitDecision> AllowAsync(string key, int limit, TimeSpan period)
    {
        var db = _mux.GetDatabase();
        var windowKey = BuildWindowKey(key, period);
        // INCR e setar TTL se for novo
        var count = await db.StringIncrementAsync(windowKey);
        if (count == 1)
        {
            await db.KeyExpireAsync(windowKey, period);
        }
        var remaining = (int)Math.Max(0, limit - count);
        int? retryAfter = null;
        if (count > limit)
        {
            var ttl = await db.KeyTimeToLiveAsync(windowKey);
            retryAfter = ttl.HasValue ? (int)Math.Max(0, ttl.Value.TotalSeconds) : null;
        }
        return new RateLimitDecision
        {
            IsAllowed = count <= limit,
            Limit = limit,
            Remaining = remaining,
            RetryAfterSeconds = retryAfter
        };
    }

    private static string BuildWindowKey(string key, TimeSpan period)
    {
        // Janela com granularidade baseada no período (minutos/segundos)
        var now = DateTimeOffset.UtcNow;
        if (period.TotalMinutes >= 1)
        {
            var bucket = now.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture);
            return $"rl:{key}:{bucket}:{(int)period.TotalMinutes}m";
        }
        else
        {
            var bucket = now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
            return $"rl:{key}:{bucket}:{(int)period.TotalSeconds}s";
        }
    }
}
