using ETHTPS.API.Cache;
using ETHTPS.API.Hubs;
using ETHTPS.API.Infrastructure;
using ETHTPS.API.Middleware;
using ETHTPS.API.Options;
using ETHTPS.API.RateLimiting;
using ETHTPS.API.Repositories;
using ETHTPS.API.Services;
using ETHTPS.API.Workers;
using Npgsql;
using Scalar.AspNetCore;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .Configure<ApiOptions>(builder.Configuration.GetSection("Api"))
    .AddSingleton(_ => NpgsqlDataSource.Create(builder.Configuration.GetConnectionString("Postgres")!))
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
        .AddStackExchangeRedis(builder.Configuration.GetConnectionString("Redis")!)
        .Services
    .AddControllers()
        .Services
    .AddRateLimiting(builder)
    .AddSingleton<SchemaInitializer>()
    .AddOpenApi();

var app = builder.Build();

await app.Services.GetRequiredService<SchemaInitializer>().RunAsync();

// API key validation middleware must run before the rate limiter
app.UseMiddleware<ApiKeyMiddleware>();
app.UseRateLimiter();

app.MapOpenApi();
app.MapControllers();
app.MapHub<MetricsHub>("/hubs/metrics");
app.MapScalarApiReference();
await app.RunAsync();
