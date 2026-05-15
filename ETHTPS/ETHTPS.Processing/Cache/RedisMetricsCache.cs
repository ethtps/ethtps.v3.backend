using System.Text.Json;
using ETHTPS.Processing.Models;
using ETHTPS.Processing.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace ETHTPS.Processing.Cache;

public class RedisMetricsCache(
    IConnectionMultiplexer redis,
    IOptions<ProcessingOptions> options,
    ILogger<RedisMetricsCache> logger) : IMetricsCache
{
    public async Task SetAsync(ComputedMetrics metrics, CancellationToken ct)
    {
        try
        {
            var db = redis.GetDatabase();
            var key = $"ethtps:live:{metrics.ChainId}";
            var value = JsonSerializer.Serialize(new
            {
                tps = metrics.Tps,
                gps = metrics.Gps,
                blockNumber = metrics.BlockNumber,
                timestamp = metrics.Timestamp
            });
            var ttl = TimeSpan.FromSeconds(options.Value.RedisLiveTtlSeconds);
            await db.StringSetAsync(key, value, ttl);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to update Redis cache for chain {ChainId}", metrics.ChainId);
        }
    }
}
