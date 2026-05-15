using System.Text.Json;
using System.Threading.Channels;
using Confluent.Kafka;
using ETHTPS.Processing.Models;
using ETHTPS.Processing.Options;
using ETHTPS.Processing.Repositories;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ETHTPS.Processing.Workers;

public class TransactionConsumer(
    TransactionRepository transactionRepository,
    IOptions<ProcessingOptions> options,
    IConfiguration configuration,
    ILogger<TransactionConsumer> logger) : BackgroundService
{
    private Channel<(RawTransaction Tx, TopicPartitionOffset Offset)>? _channel;
    private readonly List<TopicPartitionOffset> _pendingOffsets = [];
    private IConsumer<string, string>? _consumer;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = options.Value;
        _channel = Channel.CreateBounded<(RawTransaction, TopicPartitionOffset)>(
            new BoundedChannelOptions(opts.TransactionChannelCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleWriter = true
            });

        var groupId = $"processing-txs-{opts.ShardChainIdMin}-{opts.ShardChainIdMax}";
        var config = new ConsumerConfig
        {
            BootstrapServers = configuration["Kafka:BootstrapServers"],
            GroupId = groupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        _consumer = new ConsumerBuilder<string, string>(config).Build();
        _consumer.Subscribe("raw-transactions");

        var flushTask = Task.Run(() => FlushLoopAsync(stoppingToken), stoppingToken);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<string, string>? result = null;
                try
                {
                    result = _consumer.Consume(stoppingToken);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error consuming raw-transactions");
                    await Task.Delay(1000, stoppingToken);
                    continue;
                }

                if (result?.Message?.Value is null) continue;

                try
                {
                    var tx = JsonSerializer.Deserialize<RawTransaction>(result.Message.Value);
                    if (tx is null || tx.ChainId < opts.ShardChainIdMin || tx.ChainId > opts.ShardChainIdMax)
                        continue;

                    await _channel.Writer.WriteAsync((tx, result.TopicPartitionOffset), stoppingToken);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error queuing transaction");
                }
            }
        }
        finally
        {
            _channel.Writer.Complete();
            await flushTask.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            _consumer.Close();
        }
    }

    private async Task FlushLoopAsync(CancellationToken ct)
    {
        var opts = options.Value;
        var batch = new List<RawTransaction>(opts.TransactionBatchSize);
        var offsets = new List<TopicPartitionOffset>(opts.TransactionBatchSize);

        while (!ct.IsCancellationRequested || (_channel?.Reader.Count > 0))
        {
            using var flushCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            flushCts.CancelAfter(opts.TransactionFlushIntervalMs);

            try
            {
                while (batch.Count < opts.TransactionBatchSize)
                {
                    var (tx, offset) = await _channel!.Reader.ReadAsync(flushCts.Token);
                    batch.Add(tx);
                    offsets.Add(offset);
                }
            }
            catch (OperationCanceledException) { /* flush timeout or host stop */ }

            if (batch.Count == 0) continue;

            try
            {
                await transactionRepository.BulkInsertAsync(batch, ct);
                _consumer?.Commit(offsets);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Failed to bulk insert {Count} transactions", batch.Count);
            }

            batch.Clear();
            offsets.Clear();
        }
    }
}
