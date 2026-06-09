using System.Text.Json;
using ETHTPS.API.Cache;
using ETHTPS.API.Models.Responses;
using ETHTPS.API.Repositories;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace ETHTPS.API.Services;

public class MetricsQueryService(
    IQueryCache cache,
    MetricsReadRepository repo,
    IConnectionMultiplexer redis,
    ILogger<MetricsQueryService> logger)
{
    public async Task<LiveMetricsResponse?> GetLiveAsync(int chainId, CancellationToken ct)
    {
        try
        {
            var db = redis.GetDatabase();
            var value = await db.StringGetAsync($"ethtps:live:{chainId}");
            if (!value.HasValue) return null;

            using var doc = JsonDocument.Parse((string)value!);
            var root = doc.RootElement;
            return new LiveMetricsResponse(
                chainId,
                root.TryGetProperty("tps", out var tps) && tps.ValueKind != JsonValueKind.Null ? tps.GetDouble() : null,
                root.TryGetProperty("gps", out var gps) && gps.ValueKind != JsonValueKind.Null ? gps.GetDouble() : null,
                root.TryGetProperty("blockNumber", out var bn) ? bn.GetInt64() : 0,
                root.TryGetProperty("timestamp", out var ts) ? ts.GetDateTimeOffset() : DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read live metrics for chain {ChainId}", chainId);
            return null;
        }
    }

    public async Task<HistoricalMetricsResponse> GetHistoryAsync(
        int chainId, DateTimeOffset from, DateTimeOffset to, string resolution, CancellationToken ct)
    {
        var cacheKey = $"ethtps:query:{chainId}:{resolution}:{from.ToUnixTimeSeconds()}:{to.ToUnixTimeSeconds()}";
        var cached = await cache.GetAsync<HistoricalMetricsResponse>(cacheKey, ct);
        if (cached is not null) return cached;

        var buckets = await repo.GetHistoryAsync(chainId, from, to, resolution, ct);
        var result = new HistoricalMetricsResponse(chainId, resolution, from, to, buckets);

        var ttl = resolution switch
        {
            "1m" => TimeSpan.FromSeconds(30),
            "5m" => TimeSpan.FromMinutes(2),
            "1h" => TimeSpan.FromMinutes(10),
            "1d" => TimeSpan.FromHours(1),
            _ => TimeSpan.FromMinutes(1)
        };
        await cache.SetAsync(cacheKey, result, ttl, ct);
        return result;
    }

    public async Task<HistoricalMetricsResponse> GetGlobalHistoryAsync(
        DateTimeOffset from, DateTimeOffset to, string resolution, CancellationToken ct)
    {
        var cacheKey = $"ethtps:query:global:{resolution}:{from.ToUnixTimeSeconds()}:{to.ToUnixTimeSeconds()}";
        var cached = await cache.GetAsync<HistoricalMetricsResponse>(cacheKey, ct);
        if (cached is not null) return cached;

        var buckets = await repo.GetGlobalHistoryAsync(from, to, resolution, ct);
        var result = new HistoricalMetricsResponse(-1, resolution, from, to, buckets);

        var ttl = resolution switch
        {
            "1m" => TimeSpan.FromSeconds(30),
            "5m" => TimeSpan.FromMinutes(2),
            "1h" => TimeSpan.FromMinutes(10),
            "1d" => TimeSpan.FromHours(1),
            _ => TimeSpan.FromMinutes(1)
        };
        await cache.SetAsync(cacheKey, result, ttl, ct);
        return result;
    }
}
