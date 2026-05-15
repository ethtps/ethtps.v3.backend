using System.Collections.Concurrent;
using ETHTPS.Ingestion.Models;
using ETHTPS.Ingestion.Options;
using ETHTPS.Ingestion.Publishers;
using ETHTPS.Ingestion.Rpc;
using ETHTPS.Ingestion.Watchers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace ETHTPS.Ingestion.Workers;

public class IngestionOrchestrator(
    NpgsqlDataSource dataSource,
    RpcClient rpcClient,
    RpcHealthTracker healthTracker,
    RpcSelector rpcSelector,
    IBlockPublisher publisher,
    IOptions<IngestionOptions> options,
    ILogger<IngestionOrchestrator> logger,
    ILoggerFactory loggerFactory) : BackgroundService
{
    private readonly ConcurrentDictionary<int, WatcherHandle> _watchers = new();
    private CancellationToken _hostToken;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _hostToken = stoppingToken;
        var networks = await LoadNetworksFromDbAsync(stoppingToken);
        foreach (var network in networks)
            StartWatcher(network);

        await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        var stopTasks = _watchers.Values.Select(async h =>
        {
            await h.Cts.CancelAsync();
            try { await h.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken); } catch { /* graceful */ }
        });
        await Task.WhenAll(stopTasks);
        await base.StopAsync(cancellationToken);
    }

    public void StartWatcher(Network network)
    {
        if (!InShard(network.ChainId)) return;
        if (_watchers.ContainsKey(network.ChainId)) return;

        var cts = CancellationTokenSource.CreateLinkedTokenSource(_hostToken);
        var watcher = new NetworkWatcher(network, rpcClient, healthTracker, rpcSelector, publisher,
            options, loggerFactory.CreateLogger<NetworkWatcher>());
        var task = Task.Run(() => watcher.RunAsync(cts.Token), cts.Token);
        var handle = new WatcherHandle(task, cts);

        if (!_watchers.TryAdd(network.ChainId, handle))
        {
            cts.Dispose();
            logger.LogDebug("Watcher already exists for chain {ChainId}", network.ChainId);
        }
        else
        {
            logger.LogInformation("Started watcher for chain {ChainId} ({Name})", network.ChainId, network.Name);
        }
    }

    public async Task StopWatcherAsync(int chainId)
    {
        if (!_watchers.TryRemove(chainId, out var handle)) return;
        await handle.Cts.CancelAsync();
        try { await handle.Task.WaitAsync(TimeSpan.FromSeconds(5)); } catch { /* graceful */ }
        handle.Dispose();
        logger.LogInformation("Stopped watcher for chain {ChainId}", chainId);
    }

    public async Task RestartWatcherAsync(Network network)
    {
        await StopWatcherAsync(network.ChainId);
        StartWatcher(network);
    }

    private bool InShard(int chainId) =>
        chainId >= options.Value.ShardChainIdMin && chainId <= options.Value.ShardChainIdMax;

    private async Task<IReadOnlyList<Network>> LoadNetworksFromDbAsync(CancellationToken ct)
    {
        var opts = options.Value;
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT chain_id, name, rpc_urls FROM networks
            WHERE removed_at IS NULL
              AND chain_id BETWEEN $1 AND $2
            """;
        cmd.Parameters.Add(new NpgsqlParameter<int> { Value = opts.ShardChainIdMin });
        cmd.Parameters.Add(new NpgsqlParameter<int> { Value = opts.ShardChainIdMax });
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var networks = new List<Network>();
        while (await reader.ReadAsync(ct))
        {
            networks.Add(new Network
            {
                ChainId = reader.GetInt32(0),
                Name = reader.GetString(1),
                RpcUrls = reader.GetFieldValue<string[]>(2)
            });
        }
        return networks;
    }
}
