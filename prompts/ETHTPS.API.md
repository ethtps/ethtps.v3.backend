# ETHTPS.API — Implementation System Prompt

## Context
You are implementing `ETHTPS.API`, the fifth and final microservice in the ETHTPS backend. It exposes a public REST API for historical TPS/GPS queries and a SignalR WebSocket hub for real-time per-chain metric updates. Anonymous access is rate-limited by IP; API key holders get higher limits. Historical results are cached in Redis. Real-time data is consumed from Kafka and pushed to SignalR subscribers.

## Your task
Implement `ETHTPS.API` as a .NET 10 ASP.NET Core Web API. Write production-quality code with no placeholders. Implement everything fully.

---

## Project structure
```
ETHTPS.API/
  Controllers/
    NetworksController.cs
    MetricsController.cs
  Hubs/
    MetricsHub.cs
  Workers/
    MetricsEventConsumer.cs       ← BackgroundService; Kafka → SignalR push + global aggregate
  Services/
    MetricsQueryService.cs        ← orchestrates Redis cache + TimescaleDB fallback
    NetworkQueryService.cs
    ApiKeyService.cs              ← validates API keys
  Repositories/
    MetricsReadRepository.cs      ← TimescaleDB historical queries
    NetworkReadRepository.cs
    ApiKeyRepository.cs
  Cache/
    IQueryCache.cs
    RedisQueryCache.cs
  RateLimiting/
    RateLimitingExtensions.cs
  Models/
    Responses/
      NetworkResponse.cs
      LiveMetricsResponse.cs
      HistoricalBucket.cs
      HistoricalMetricsResponse.cs
      GlobalMetricsResponse.cs
    Messages/
      MetricsUpdateMessage.cs     ← SignalR push payload
  Options/
    ApiOptions.cs
  Infrastructure/
    SchemaInitializer.cs
  Program.cs
```

---

## REST endpoints

All routes are prefixed `/api/v1`.

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/networks` | Anonymous | List all active networks |
| GET | `/networks/{chainId}` | Anonymous | Single network by chain ID |
| GET | `/metrics/{chainId}/live` | Anonymous | Latest TPS + GPS for one chain (from Redis) |
| GET | `/metrics/{chainId}/history` | Anonymous | Historical buckets for one chain |
| GET | `/metrics/global/live` | Anonymous | Sum of TPS + GPS across all chains (in-memory) |
| GET | `/metrics/global/history` | Anonymous | Historical global aggregate buckets |

### `/metrics/{chainId}/history` query parameters
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `from` | ISO 8601 datetime | Yes | Range start (inclusive) |
| `to` | ISO 8601 datetime | Yes | Range end (inclusive) |
| `resolution` | `1m` \| `5m` \| `1h` \| `1d` | Yes | Bucket size |

Enforce max query range per resolution to prevent abuse:
- `1m` → max 24 hours
- `5m` → max 7 days
- `1h` → max 90 days
- `1d` → 5 years (no practical limit)

Return `400 Bad Request` with a descriptive `{ "error": "..." }` body if the range exceeds the limit or parameters are invalid.

---

## Response models

```csharp
public record NetworkResponse(
    int ChainId, string Name, string[] RpcUrls, bool Enabled);

public record LiveMetricsResponse(
    int ChainId, double? Tps, double? Gps, long BlockNumber, DateTimeOffset Timestamp);

public record HistoricalBucket(
    DateTimeOffset Bucket,
    double? AvgTps, double? MaxTps,
    double? AvgGps, double? MaxGps,
    int BlockCount);

public record HistoricalMetricsResponse(
    int ChainId, string Resolution,
    DateTimeOffset From, DateTimeOffset To,
    IReadOnlyList<HistoricalBucket> Buckets);

public record GlobalMetricsResponse(
    double TotalTps, double TotalGps,
    int ActiveChains, DateTimeOffset ComputedAt);

// SignalR push payload
public record MetricsUpdateMessage(
    int ChainId, double? Tps, double? Gps,
    long BlockNumber, DateTimeOffset Timestamp);
```

---

## MetricsHub
```csharp
[Authorize(Policy = "HubAccess")]   // allow anonymous — see rate limiting section
public class MetricsHub : Hub
{
    // Client calls this to receive updates for specific chains
    public async Task Subscribe(int[] chainIds)
    {
        foreach (var id in chainIds)
            await Groups.AddToGroupAsync(Context.ConnectionId, $"chain:{id}");
    }

    public async Task Unsubscribe(int[] chainIds)
    {
        foreach (var id in chainIds)
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"chain:{id}");
    }
}
```
Map at `/hubs/metrics`. Allow anonymous connections. SignalR uses WebSockets with long-polling fallback.

---

## MetricsEventConsumer
A `BackgroundService` that:
1. Consumes Kafka topic `aggregated-metrics`, group id `api-signalr`.
2. Deserialises each message as `MetricsComputedEvent` (same shape as published by `ETHTPS.Processing`).
3. Pushes a `MetricsUpdateMessage` to the SignalR group `chain:{chainId}` via `IHubContext<MetricsHub>`.
4. Maintains an in-memory `ConcurrentDictionary<int, (double Tps, double Gps, DateTimeOffset Timestamp)>` of the latest live value per chain — used to serve `GET /metrics/global/live` without a DB round-trip.
5. Exposes a thread-safe `GetGlobalSnapshot()` method returning the sum of all chains' latest non-null TPS and GPS values, the count of active chains (those with a value newer than 60 seconds), and the timestamp of the most recent update.
6. Commits Kafka offsets manually after each message is processed and pushed.

Register `MetricsEventConsumer` as both a `BackgroundService` and a singleton so controllers can call `GetGlobalSnapshot()`.

---

## Rate limiting

Use `Microsoft.AspNetCore.RateLimiting`. Two policies:

| Policy | Limit | Window | Keyed by |
|--------|-------|--------|----------|
| `anonymous` | 60 requests | 1 minute sliding | Client IP (`X-Forwarded-For` or `RemoteIpAddress`) |
| `apikey` | 600 requests | 1 minute sliding | API key value |

### Middleware pipeline
Add a custom `IHttpContextAccessor`-based selector that:
1. Reads `X-Api-Key` header (or `?apiKey=` query param as fallback).
2. If present: validates via `ApiKeyService` (checks DB, uses Redis to cache the result for 5 minutes). If valid → apply `apikey` policy keyed by the hash of the key. If invalid → return `401 Unauthorized`.
3. If absent → apply `anonymous` policy keyed by IP.

Apply the rate limiter globally to all controller routes and the SignalR hub endpoint.

Return `429 Too Many Requests` with header `Retry-After` (seconds until window resets) when limits are exceeded.

---

## API key schema and service

### `api_keys` table (created by `SchemaInitializer`)
```sql
CREATE TABLE IF NOT EXISTS api_keys (
    key_hash    TEXT        PRIMARY KEY,    -- SHA-256(key), hex-encoded
    name        TEXT        NOT NULL,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
    expires_at  TIMESTAMPTZ,
    enabled     BOOLEAN     NOT NULL DEFAULT TRUE
);
```
Never store the raw key. Store `SHA256(key)` hex-encoded.

### `ApiKeyService`
```csharp
public class ApiKeyService(ApiKeyRepository repo, IConnectionMultiplexer redis)
{
    // Returns true if the key exists, is enabled, and is not expired
    public async Task<bool> ValidateAsync(string rawKey, CancellationToken ct)
    {
        var hash = ComputeHash(rawKey);
        // Check Redis cache first (key: ethtps:apikey:{hash}, TTL 5m)
        // On miss: query DB, write result to Redis
        ...
    }

    private static string ComputeHash(string key) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant();
}
```

---

## MetricsQueryService

Orchestrates the cache-first data access pattern:

```csharp
public class MetricsQueryService(
    IQueryCache cache,
    MetricsReadRepository repo,
    IConnectionMultiplexer redis)
{
    public async Task<LiveMetricsResponse?> GetLiveAsync(int chainId, CancellationToken ct)
    {
        // Read from Redis key ethtps:live:{chainId} (written by ETHTPS.Processing)
        // Return null if key missing or expired
        ...
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
            _    => TimeSpan.FromMinutes(1)
        };
        await cache.SetAsync(cacheKey, result, ttl, ct);
        return result;
    }
}
```

---

## MetricsReadRepository

Queries TimescaleDB continuous aggregate views for historical data:

```csharp
public async Task<IReadOnlyList<HistoricalBucket>> GetHistoryAsync(
    int chainId, DateTimeOffset from, DateTimeOffset to, string resolution, CancellationToken ct)
```

Resolution → view name mapping:
| Resolution | View |
|------------|------|
| `1m` | `metrics_1m` |
| `5m` | `metrics_5m` |
| `1h` | `metrics_1h` |
| `1d` | `metrics_1d` |

SQL pattern (parameterised):
```sql
SELECT bucket, avg_tps, max_tps, avg_gps, max_gps, block_count
FROM {viewName}
WHERE chain_id = $1 AND bucket >= $2 AND bucket <= $3
ORDER BY bucket ASC;
```

For global history (`chainId = -1` sentinel or a dedicated method): replace `WHERE chain_id = $1` with a `GROUP BY bucket` aggregate across all chains:
```sql
SELECT bucket,
       sum(avg_tps)  AS avg_tps,
       max(max_tps)  AS max_tps,
       sum(avg_gps)  AS avg_gps,
       max(max_gps)  AS max_gps,
       sum(block_count) AS block_count
FROM {viewName}
WHERE bucket >= $1 AND bucket <= $2
GROUP BY bucket
ORDER BY bucket ASC;
```

Use raw Npgsql. No EF Core. No Dapper.

---

## RedisQueryCache

```csharp
public interface IQueryCache
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct);
    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct);
}
```
Use `IConnectionMultiplexer`. Serialise values with `System.Text.Json`. Return `null` on cache miss or deserialisation failure (never throw from cache reads).

---

## Database schema (`SchemaInitializer`)
Creates only what this service owns:
```sql
CREATE TABLE IF NOT EXISTS api_keys (
    key_hash    TEXT        PRIMARY KEY,
    name        TEXT        NOT NULL,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
    expires_at  TIMESTAMPTZ,
    enabled     BOOLEAN     NOT NULL DEFAULT TRUE
);
```
Does **not** create `blocks`, `metrics`, `networks`, or their hypertables — those are owned by `ETHTPS.Processing` and `ETHTPS.Backfill`. The API is read-only against those tables.

---

## Options
```csharp
public class ApiOptions
{
    public int AnonymousRateLimitPerMinute { get; set; } = 60;
    public int ApiKeyRateLimitPerMinute { get; set; } = 600;
    public int ApiKeyCacheTtlSeconds { get; set; } = 300;
}
```

---

## Program.cs
```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .Configure<ApiOptions>(builder.Configuration.GetSection("Api"))
    .AddNpgsqlDataSource(builder.Configuration.GetConnectionString("Postgres")!)
    .AddSingleton<IConnectionMultiplexer>(_ =>
        ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")!))
    .AddSingleton<IQueryCache, RedisQueryCache>()
    .AddSingleton<ApiKeyService>()
    .AddSingleton<ApiKeyRepository>()
    .AddSingleton<MetricsReadRepository>()
    .AddSingleton<NetworkReadRepository>()
    .AddSingleton<MetricsQueryService>()
    .AddSingleton<NetworkQueryService>()
    .AddSingleton<MetricsEventConsumer>()
    .AddHostedService(sp => sp.GetRequiredService<MetricsEventConsumer>())
    .AddSignalR()
    .AddStackExchangeRedis(builder.Configuration.GetConnectionString("Redis")!)  // SignalR Redis backplane for multi-replica
    .Services
    .AddControllers()
    .Services
    .AddRateLimiting(builder)   // extension method in RateLimitingExtensions.cs
    .AddSingleton<SchemaInitializer>();

var app = builder.Build();

await app.Services.GetRequiredService<SchemaInitializer>().RunAsync();

app.UseRateLimiter();
app.MapControllers();
app.MapHub<MetricsHub>("/hubs/metrics");

await app.RunAsync();
```

Note: SignalR is backed by a Redis backplane (`AddStackExchangeRedis`) so that multiple API replicas can push to the same client regardless of which replica the client is connected to.

---

## appsettings.json shape
```json
{
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Database=ethtps;Username=ethtps;Password=ethtps",
    "Redis": "localhost:6379"
  },
  "Kafka": {
    "BootstrapServers": "localhost:9092"
  },
  "Api": {
    "AnonymousRateLimitPerMinute": 60,
    "ApiKeyRateLimitPerMinute": 600,
    "ApiKeyCacheTtlSeconds": 300
  }
}
```

---

## NuGet packages
- `Npgsql` — PostgreSQL read queries
- `Confluent.Kafka` — Kafka consumer
- `StackExchange.Redis` — Redis cache + SignalR backplane
- `Microsoft.AspNetCore.SignalR.StackExchangeRedis` — SignalR Redis backplane
- `Microsoft.AspNetCore.RateLimiting` — in-box rate limiting
- `System.Text.Json` — serialisation
- `Microsoft.Extensions.Http.Resilience` — not needed here (no outbound HTTP); omit

---

## Constraints and rules
- Target **.NET 10**. Use modern C# throughout.
- No EF Core. No Dapper. Raw Npgsql for all DB access.
- All async methods accept and forward `CancellationToken`.
- Cache reads must never throw — catch all exceptions, log at `Warning`, and fall through to the DB.
- `MetricsEventConsumer.GetGlobalSnapshot()` must be thread-safe — it is called from controller threads concurrently with Kafka consumer updates.
- API key validation results must be cached in Redis, not in-process — the API may run as multiple replicas.
- `X-Forwarded-For` must be trusted only if the request arrives through a known proxy — read `KnownProxies` from config and configure `ForwardedHeadersOptions` accordingly.
- All controller actions must return `application/json`. Use `Results.Problem(...)` for error responses to produce RFC 9110-compliant problem details.
- SignalR group names are always `chain:{chainId}` — never expose raw connection IDs to clients.
- Log with `ILogger<T>` using structured parameters. Include `chainId` in all metric-related log entries.