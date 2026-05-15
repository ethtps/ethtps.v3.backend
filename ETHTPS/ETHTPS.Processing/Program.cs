using Npgsql;
using StackExchange.Redis;
using ETHTPS.Processing.Cache;
using ETHTPS.Processing.Infrastructure;
using ETHTPS.Processing.Options;
using ETHTPS.Processing.Publishers;
using ETHTPS.Processing.Repositories;
using ETHTPS.Processing.Workers;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .Configure<ProcessingOptions>(builder.Configuration.GetSection("Processing"))
    .AddSingleton(_ => NpgsqlDataSource.Create(builder.Configuration.GetConnectionString("Postgres")!))
    .AddSingleton<IConnectionMultiplexer>(_ =>
        ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")!))
    .AddSingleton<IMetricsCache, RedisMetricsCache>()
    .AddSingleton<IMetricsPublisher, KafkaMetricsPublisher>()
    .AddSingleton<BlockRepository>()
    .AddSingleton<MetricsRepository>()
    .AddSingleton<TransactionRepository>()
    .AddSingleton<SchemaInitializer>()
    .AddHostedService<BlockConsumer>()
    .AddHostedService<TransactionConsumer>();

var app = builder.Build();
await app.Services.GetRequiredService<SchemaInitializer>().RunAsync();
await app.RunAsync();
