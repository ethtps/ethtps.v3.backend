using Npgsql;
using ETHTPS.Ingestion.Options;
using ETHTPS.Ingestion.Publishers;
using ETHTPS.Ingestion.Rpc;
using ETHTPS.Ingestion.Workers;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .Configure<IngestionOptions>(builder.Configuration.GetSection("Ingestion"))
    .AddSingleton(_ => NpgsqlDataSource.Create(builder.Configuration.GetConnectionString("Postgres")!))
    .AddHttpClient<RpcClient>()
        .AddStandardResilienceHandler()
        .Services
    .AddSingleton<RpcHealthTracker>()
    .AddSingleton<RpcSelector>()
    .AddSingleton<IBlockPublisher, KafkaBlockPublisher>()
    .AddSingleton<IngestionOrchestrator>()
    .AddHostedService(sp => sp.GetRequiredService<IngestionOrchestrator>())
    .AddHostedService<NetworkEventConsumer>();

builder.Build().Run();
