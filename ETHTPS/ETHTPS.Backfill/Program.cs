using Npgsql;
using ETHTPS.Backfill.Infrastructure;
using ETHTPS.Backfill.Options;
using ETHTPS.Backfill.Repositories;
using ETHTPS.Backfill.Rpc;
using ETHTPS.Backfill.Workers;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .Configure<BackfillOptions>(builder.Configuration.GetSection("Backfill"))
    .AddSingleton(_ => NpgsqlDataSource.Create(builder.Configuration.GetConnectionString("Postgres")!))
    .AddHttpClient<RpcClient>()
        .AddStandardResilienceHandler()
        .Services
    .AddSingleton<RpcHealthTracker>()
    .AddSingleton<RpcSelector>()
    .AddSingleton<BackfillProgressRepository>()
    .AddSingleton<NetworkRepository>()
    .AddSingleton<BlockWriteRepository>()
    .AddSingleton<TransactionWriteRepository>()
    .AddSingleton<MetricsWriteRepository>()
    .AddSingleton<SchemaInitializer>()
    .AddSingleton<BackfillOrchestrator>()
    .AddHostedService(sp => sp.GetRequiredService<BackfillOrchestrator>())
    .AddHostedService<NetworkEventConsumer>();

var app = builder.Build();
await app.Services.GetRequiredService<SchemaInitializer>().RunAsync();
await app.RunAsync();
