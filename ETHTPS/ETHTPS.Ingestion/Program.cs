using Npgsql;
using ETHTPS.Ingestion;
using ETHTPS.Ingestion.Options;
using ETHTPS.Ingestion.Publishers;
using ETHTPS.Ingestion.Rpc;
using ETHTPS.Ingestion.RpcOverrides;
using ETHTPS.Ingestion.Workers;
using StackExchange.Redis;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();

builder.Services
    .Configure<IngestionOptions>(builder.Configuration.GetSection("Ingestion"))
    .AddSingleton(_ => NpgsqlDataSource.Create(builder.Configuration.GetConnectionString("Postgres")!))
    .AddSingleton<IConnectionMultiplexer>(_ =>
        ConnectionMultiplexer.Connect(
            builder.Configuration.GetConnectionString("Redis")
            ?? throw new InvalidOperationException("ConnectionStrings:Redis is not configured.")))
    .AddHttpClient<RpcClient>()
        .Services
    .AddSingleton<RpcHealthTracker>()
    .AddSingleton<RpcSelector>()
    .AddSingleton<IBlockPublisher, KafkaBlockPublisher>()
    .AddSingleton<RpcOverridesLoader>()
    .AddSingleton<StatusTracker>()
    .AddSingleton<IngestionOrchestrator>()
    .AddHostedService(sp => sp.GetRequiredService<IngestionOrchestrator>())
    .AddHostedService<NetworkEventConsumer>();

builder.Build().Run();
