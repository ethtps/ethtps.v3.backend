using System.Security.Cryptography;
using System.Text;
using ETHTPS.API.Options;
using ETHTPS.API.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace ETHTPS.API.Services;

public class ApiKeyService(
    ApiKeyRepository repo,
    IConnectionMultiplexer redis,
    IOptions<ApiOptions> options,
    ILogger<ApiKeyService> logger)
{
    public async Task<bool> ValidateAsync(string rawKey, CancellationToken ct)
    {
        var hash = ComputeHash(rawKey);
        var cacheKey = $"ethtps:apikey:{hash}";
        try
        {
            var db = redis.GetDatabase();
            var cached = await db.StringGetAsync(cacheKey);
            if (cached.HasValue) return cached == "1";

            var apiKey = await repo.FindByHashAsync(hash, ct);
            var valid = apiKey is { Enabled: true } &&
                        (apiKey.ExpiresAt is null || apiKey.ExpiresAt > DateTimeOffset.UtcNow);

            await db.StringSetAsync(cacheKey, valid ? "1" : "0",
                TimeSpan.FromSeconds(options.Value.ApiKeyCacheTtlSeconds));

            return valid;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "API key validation error, falling back to DB");
            var apiKey = await repo.FindByHashAsync(hash, ct);
            return apiKey is { Enabled: true } &&
                   (apiKey.ExpiresAt is null || apiKey.ExpiresAt > DateTimeOffset.UtcNow);
        }
    }

    public static string ComputeHash(string key) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant();
}
