using System.Text.Json;
using Confluent.Kafka;
using ETHTPS.Processing.Cache;
using ETHTPS.Processing.Models;
using ETHTPS.Processing.Options;
using ETHTPS.Processing.Processors;
using ETHTPS.Processing.Publishers;
using ETHTPS.Processing.Repositories;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ETHTPS.Processing.Workers;

public class BlockConsumer(
    BlockRepository blockRepository,
    MetricsRepository metricsRepository,
    IMetricsCache metricsCache,
    IMetricsPublisher metricsPublisher,
    IOptions<ProcessingOptions> options,
    IConfiguration configuration,
    ILogger<BlockConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = options.Value;
        var groupId = $"processing-blocks-{opts.ShardChainIdMin}-{opts.ShardChainIdMax}";
        var config = new ConsumerConfig
        {
            BootstrapServers = configuration["Kafka:BootstrapServers"],
            GroupId = groupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe("raw-blocks");

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
                    logger.LogError(ex, "Error consuming raw-blocks");
                    await Task.Delay(1000, stoppingToken);
                    continue;
                }

                if (result?.Message?.Value is null) continue;

                RawBlock? block = null;
                try
                {
                    block = JsonSerializer.Deserialize<RawBlock>(result.Message.Value);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to deserialize RawBlock");
                    consumer.Commit(result);
                    continue;
                }

                if (block is null || block.ChainId < opts.ShardChainIdMin || block.ChainId > opts.ShardChainIdMax)
                {
                    consumer.Commit(result);
                    continue;
                }

                try
                {
                    var metrics = MetricsProcessor.Compute(block);
                    await blockRepository.InsertAsync(block, stoppingToken);
                    await metricsRepository.InsertAsync(metrics, stoppingToken);
                    await metricsCache.SetAsync(metrics, stoppingToken);
                    await metricsPublisher.PublishAsync(new MetricsComputedEvent
                    {
                        ChainId = metrics.ChainId,
                        BlockNumber = metrics.BlockNumber,
                        Timestamp = metrics.Timestamp,
                        Tps = metrics.Tps,
                        Gps = metrics.Gps,
                        ComputedAt = DateTimeOffset.UtcNow
                    }, stoppingToken);

                    consumer.Commit(result);
                    logger.LogDebug("Processed block {BlockNumber} chain {ChainId} tps={Tps} gps={Gps}",
                        block.BlockNumber, block.ChainId, metrics.Tps, metrics.Gps);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Error processing block {BlockNumber} chain {ChainId}", block.BlockNumber, block.ChainId);
                }
            }
        }
        finally
        {
            consumer.Close();
        }
    }
}
