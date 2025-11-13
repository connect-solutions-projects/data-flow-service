using System;
using System.Threading.Tasks;
using DataFlow.Api.Services.Interfaces;
using StackExchange.Redis;

namespace DataFlow.Api.Services;

public class RedisLockService : IRedisLockService
{
    private readonly IConnectionMultiplexer _mux;
    private readonly IDatabase _db;

    private const string LuaRelease = @"
if redis.call('get', KEYS[1]) == ARGV[1] then
    return redis.call('del', KEYS[1])
else
    return 0
end";

    public RedisLockService(IConnectionMultiplexer mux)
    {
        _mux = mux;
        _db = _mux.GetDatabase();
    }

    public async Task<string?> AcquireAsync(string key, TimeSpan ttl)
    {
        var token = Guid.NewGuid().ToString("N");
        var acquired = await _db.StringSetAsync(key, token, ttl, When.NotExists);
        return acquired ? token : null;
    }

    public async Task<bool> ReleaseAsync(string key, string token)
    {
        var result = (int) (await _db.ScriptEvaluateAsync(LuaRelease, new RedisKey[] { key }, new RedisValue[] { token }));
        return result == 1;
    }
}

