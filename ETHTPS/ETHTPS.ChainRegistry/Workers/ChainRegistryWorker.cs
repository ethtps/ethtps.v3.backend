using ETHTPS.ChainRegistry.Clients;
using ETHTPS.ChainRegistry.Events;
using ETHTPS.ChainRegistry.Kafka;
using ETHTPS.ChainRegistry.Models;
using ETHTPS.ChainRegistry.Options;
using ETHTPS.ChainRegistry.Repositories;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ETHTPS.ChainRegistry.Workers;

public class ChainRegistryWorker(
    ChainlistClient chainlistClient,
    INetworkRepository networkRepository,
    IKafkaProducer kafkaProducer,
    IOptions<ChainRegistryOptions> options,
    ILogger<ChainRegistryWorker> logger) : BackgroundService
{
    private const string NetworkEventsTopic = "network-events";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await SyncAsync(stoppingToken);
        using var timer = new PeriodicTimer(options.Value.SyncInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await SyncAsync(stoppingToken);
        }
    }

    private async Task SyncAsync(CancellationToken ct)
    {
        var chainlistNetworks = await chainlistClient.FetchNetworksAsync(ct);
        if (chainlistNetworks is null)
        {
            logger.LogWarning("Chainlist fetch returned null — skipping sync cycle");
            return;
        }

        var dbNetworks = await networkRepository.GetAllAsync(ct);
        var dbMap = dbNetworks.ToDictionary(n => n.ChainId);
        var chainlistMap = chainlistNetworks.ToDictionary(n => n.ChainId);

        var added = 0;
        var removed = 0;
        var updated = 0;
        var now = DateTimeOffset.UtcNow;

        foreach (var (chainId, clNetwork) in chainlistMap)
        {
            var filteredRpcs = ChainlistClient.FilterUsableRpcs(clNetwork.Rpc);
            if (!dbMap.TryGetValue(chainId, out var existing) || existing.RemovedAt.HasValue)
            {
                var network = new Network
                {
                    ChainId = chainId,
                    Name = clNetwork.Name,
                    RpcUrls = filteredRpcs,
                    Enabled = true,
                    RemovedAt = null,
                    LastSyncedAt = now
                };
                await networkRepository.UpsertAsync(network, ct);
                await kafkaProducer.PublishAsync(NetworkEventsTopic, chainId.ToString(),
                    new NetworkAddedEvent(chainId, clNetwork.Name, filteredRpcs, now), ct);
                added++;
            }
            else if (!filteredRpcs.SequenceEqual(existing.RpcUrls))
            {
                var network = existing with { RpcUrls = filteredRpcs, LastSyncedAt = now };
                await networkRepository.UpsertAsync(network, ct);
                await kafkaProducer.PublishAsync(NetworkEventsTopic, chainId.ToString(),
                    new NetworkRpcsUpdatedEvent(chainId, filteredRpcs, now), ct);
                updated++;
            }
            else
            {
                await networkRepository.UpsertAsync(existing with { LastSyncedAt = now }, ct);
            }
        }

        foreach (var (chainId, dbNetwork) in dbMap)
        {
            if (!chainlistMap.ContainsKey(chainId) && !dbNetwork.RemovedAt.HasValue)
            {
                await networkRepository.MarkRemovedAsync(chainId, ct);
                await kafkaProducer.PublishAsync(NetworkEventsTopic, chainId.ToString(),
                    new NetworkRemovedEvent(chainId, now), ct);
                removed++;
            }
        }

        logger.LogInformation("Sync complete — added={Added} removed={Removed} updated={Updated}", added, removed, updated);
    }
}
