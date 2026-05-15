using System.Text.Json;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace ETHTPS.API.Cache;

public class RedisQueryCache(IConnectionMultiplexer redis, ILogger<RedisQueryCache> logger) : IQueryCache
{
    public async Task<T?> GetAsync<T>(string key, CancellationToken ct)
    {
        try
        {
            var db = redis.GetDatabase();
            var value = await db.StringGetAsync(key);
            if (!value.HasValue) return default;
            return JsonSerializer.Deserialize<T>((string)value!);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Cache read failed for key {Key}", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct)
    {
        try
        {
            var db = redis.GetDatabase();
            var json = JsonSerializer.Serialize(value);
            await db.StringSetAsync(key, json, ttl);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Cache write failed for key {Key}", key);
        }
    }
}
