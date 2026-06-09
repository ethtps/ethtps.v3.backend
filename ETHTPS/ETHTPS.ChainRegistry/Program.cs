using Npgsql;
using ETHTPS.ChainRegistry.Clients;
using ETHTPS.ChainRegistry.Kafka;
using ETHTPS.ChainRegistry.Options;
using ETHTPS.ChainRegistry.Repositories;
using ETHTPS.ChainRegistry.Workers;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .Configure<ChainRegistryOptions>(builder.Configuration.GetSection("ChainRegistry"))
    .AddSingleton(sp =>
    {
        var conn = builder.Configuration.GetConnectionString("Postgres")!;
        return NpgsqlDataSource.Create(conn);
    })
    .AddHttpClient<ChainlistClient>()
        .AddStandardResilienceHandler()
        .Services
    .AddHttpClient<L2BeatClient>()
        .AddStandardResilienceHandler()
        .Services
    .AddHttpClient<LogoFetcher>()
        .AddStandardResilienceHandler()
        .Services
    .AddSingleton<INetworkRepository, NetworkRepository>()
    .AddSingleton<IKafkaProducer, KafkaProducer>()
    .AddHostedService<ChainRegistryWorker>();

var app = builder.Build();
await app.Services.GetRequiredService<INetworkRepository>().EnsureSchemaAsync(CancellationToken.None);
app.Run();
