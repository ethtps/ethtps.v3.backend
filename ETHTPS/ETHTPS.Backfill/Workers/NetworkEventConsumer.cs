using System.Text.Json;
using Confluent.Kafka;
using ETHTPS.Backfill.Models;
using ETHTPS.Backfill.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ETHTPS.Backfill.Workers;

public class NetworkEventConsumer(
    BackfillOrchestrator orchestrator,
    IOptions<BackfillOptions> options,
    IConfiguration configuration,
    ILogger<NetworkEventConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = options.Value;
        var groupId = $"backfill-shard-{opts.ShardChainIdMin}-{opts.ShardChainIdMax}";
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
        if (!doc.RootElement.TryGetProperty("type", out var typeProp)) return;
        var type = typeProp.GetString();
        var opts = options.Value;

        switch (type)
        {
            case "NetworkAddedEvent":
            {
                var evt = JsonSerializer.Deserialize<NetworkAddedEvent>(json);
                if (evt is null || evt.ChainId < opts.ShardChainIdMin || evt.ChainId > opts.ShardChainIdMax) break;
                var network = new Network { ChainId = evt.ChainId, Name = evt.Name, RpcUrls = evt.RpcUrls };
                var progress = new BackfillProgress { ChainId = evt.ChainId };
                await orchestrator.StartCrawlerAsync(network, progress, ct);
                break;
            }
            case "NetworkRemovedEvent":
            {
                var evt = JsonSerializer.Deserialize<NetworkRemovedEvent>(json);
                if (evt is null || evt.ChainId < opts.ShardChainIdMin || evt.ChainId > opts.ShardChainIdMax) break;
                orchestrator.StopCrawler(evt.ChainId);
                break;
            }
            // NetworkRpcsUpdatedEvent is ignored — crawler picks up new RPCs naturally
        }
    }
}
