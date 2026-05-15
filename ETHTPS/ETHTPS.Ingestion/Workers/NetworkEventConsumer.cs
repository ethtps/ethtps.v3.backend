using System.Text.Json;
using Confluent.Kafka;
using ETHTPS.Ingestion.Models;
using ETHTPS.Ingestion.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ETHTPS.Ingestion.Workers;

public class NetworkEventConsumer(
    IngestionOrchestrator orchestrator,
    IOptions<IngestionOptions> options,
    IConfiguration configuration,
    ILogger<NetworkEventConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = options.Value;
        var groupId = $"ingestion-shard-{opts.ShardChainIdMin}-{opts.ShardChainIdMax}";
        var config = new ConsumerConfig
        {
            BootstrapServers = configuration["Kafka:BootstrapServers"],
            GroupId = groupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe("network-events");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<string, string>? result = null;
                try
                {
                    result = consumer.Consume(stoppingToken);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error consuming from network-events");
                    await Task.Delay(1000, stoppingToken);
                    continue;
                }

                if (result?.Message?.Value is null) continue;

                try
                {
                    await ProcessMessageAsync(result.Message.Value, stoppingToken);
                    consumer.Commit(result);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error processing network event");
                }
            }
        }
        finally
        {
            consumer.Close();
        }
    }

    private async Task ProcessMessageAsync(string json, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("type", out var typeProp)) return;
        var type = typeProp.GetString();

        switch (type)
        {
            case "NetworkAddedEvent":
            {
                var evt = JsonSerializer.Deserialize<NetworkAddedEvent>(json);
                if (evt is null || !InShard(evt.ChainId)) break;
                orchestrator.StartWatcher(new Network
                {
                    ChainId = evt.ChainId,
                    Name = evt.Name,
                    RpcUrls = evt.RpcUrls
                });
                break;
            }
            case "NetworkRemovedEvent":
            {
                var evt = JsonSerializer.Deserialize<NetworkRemovedEvent>(json);
                if (evt is null || !InShard(evt.ChainId)) break;
                await orchestrator.StopWatcherAsync(evt.ChainId);
                break;
            }
            case "NetworkRpcsUpdatedEvent":
            {
                var evt = JsonSerializer.Deserialize<NetworkRpcsUpdatedEvent>(json);
                if (evt is null || !InShard(evt.ChainId)) break;
                await orchestrator.RestartWatcherAsync(new Network
                {
                    ChainId = evt.ChainId,
                    RpcUrls = evt.NewRpcUrls
                });
                break;
            }
        }
    }

    private bool InShard(int chainId)
    {
        var opts = options.Value;
        return chainId >= opts.ShardChainIdMin && chainId <= opts.ShardChainIdMax;
    }
}
