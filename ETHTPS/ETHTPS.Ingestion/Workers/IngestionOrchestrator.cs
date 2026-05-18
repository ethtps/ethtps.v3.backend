using System.Collections.Concurrent;
using System.Text.Json;
using ETHTPS.Ingestion.Models;
using ETHTPS.Ingestion.Options;
using ETHTPS.Ingestion.Publishers;
using ETHTPS.Ingestion.Rpc;
using ETHTPS.Ingestion.RpcOverrides;
using ETHTPS.Ingestion.Watchers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using StackExchange.Redis;

namespace ETHTPS.Ingestion.Workers;

public class IngestionOrchestrator(
    NpgsqlDataSource dataSource,
    RpcClient rpcClient,
    RpcHealthTracker healthTracker,
    RpcSelector rpcSelector,
    IBlockPublisher publisher,
    RpcOverridesLoader rpcOverridesLoader,
    StatusTracker statusTracker,
    IConnectionMultiplexer redis,
    IOptions<IngestionOptions> options,
    ILogger<IngestionOrchestrator> logger,
    ILoggerFactory loggerFactory) : BackgroundService
{
    private readonly ConcurrentDictionary<int, WatcherHandle> _watchers = new();
    private CancellationToken _hostToken;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _hostToken = stoppingToken;
        var rpcOverrides = rpcOverridesLoader.Load();
        var networks = await LoadNetworksFromDbAsync(stoppingToken, rpcOverrides);
        logger.LogInformation("Loaded {Count} networks from DB — starting watchers", networks.Count);

        foreach (var network in networks)
        {
            statusTracker.Update(new ChainStatus(network.ChainId, network.Name, 0, 0, 0, DateTimeOffset.MinValue, "pending"));
            StartWatcher(network);
        }

        logger.LogInformation("All watchers started ({Count} active)", _watchers.Count);

        while (!stoppingToken.IsCancellationRequested)
        {
            var chains = statusTracker.GetAll();
            PrintStatus(chains);
            _ = PublishStatusAsync(chains);
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }
    }

    private async Task PublishStatusAsync(IReadOnlyList<ChainStatus> chains)
    {
        try
        {
            var db = redis.GetDatabase();
            var json = JsonSerializer.Serialize(chains);
            await db.StringSetAsync("ethtps:ingestion:status", json, TimeSpan.FromSeconds(30));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to publish ingestion status to Redis");
        }
    }

    private void PrintStatus(IReadOnlyList<ChainStatus> chains)
    {
        var now = DateTimeOffset.UtcNow;
        const int sep = 90;
        var line = new string('─', sep);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine(line);
        sb.AppendLine($" Ingestion  {now:HH:mm:ss} UTC  ·  {chains.Count} chains monitored");
        sb.AppendLine(line);
        sb.AppendLine($" {"#",-9} {"Name",-30} {"Block",12}  {"Txs",6}  {"BlkTime",8}  {"Last seen",10}  State");
        sb.AppendLine(line);

        foreach (var c in chains)
        {
            var blockNum = c.LastBlockNumber > 0 ? c.LastBlockNumber.ToString("N0") : "—";
            var txs      = c.LastBlockNumber > 0 ? c.LastTxCount.ToString() : "—";
            var blkTime  = c.LastBlockTimeMs > 0 ? $"{c.LastBlockTimeMs / 1000.0:F1}s" : "—";
            var age      = c.LastBlockAt == DateTimeOffset.MinValue
                ? "pending"
                : FormatAge(now - c.LastBlockAt);
            var stale    = c.State == "ok" && now - c.LastBlockAt > TimeSpan.FromSeconds(120);
            var state    = stale ? "stale" : c.State;

            var name = c.Name.Length > 29 ? c.Name[..29] + "…" : c.Name;
            sb.AppendLine($" #{c.ChainId,-8} {name,-30} {blockNum,12}  {txs,6}  {blkTime,8}  {age,10}  {state}");
        }

        sb.AppendLine(line);
        Console.Write(sb.ToString());
    }

    private static string FormatAge(TimeSpan age)
    {
        if (age.TotalSeconds < 60)  return $"{(int)age.TotalSeconds}s ago";
        if (age.TotalMinutes < 60)  return $"{(int)age.TotalMinutes}m ago";
        return $"{(int)age.TotalHours}h ago";
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
            options, statusTracker, loggerFactory.CreateLogger<NetworkWatcher>());
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

    private async Task<IReadOnlyList<Network>> LoadNetworksFromDbAsync(
        CancellationToken ct, IReadOnlyDictionary<string, string[]>? rpcOverrides = null)
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
            var name = reader.GetString(1);
            var rpcUrls = reader.GetFieldValue<string[]>(2);

            if (rpcOverrides is not null &&
                rpcOverrides.TryGetValue(name.ToLowerInvariant(), out var extra))
            {
                rpcUrls = [..rpcUrls, ..extra.Except(rpcUrls)];
                logger.LogInformation("Applied {Count} RPC override(s) to {Name}", extra.Length, name);
            }

            networks.Add(new Network
            {
                ChainId = reader.GetInt32(0),
                Name = name,
                RpcUrls = rpcUrls
            });
        }
        return networks;
    }
}
