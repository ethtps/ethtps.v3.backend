using System.Text.Json;
using Confluent.Kafka;
using ETHTPS.Ingestion.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ETHTPS.Ingestion.Publishers;

public sealed class KafkaBlockPublisher : IBlockPublisher, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaBlockPublisher> _logger;

    public KafkaBlockPublisher(IConfiguration configuration, ILogger<KafkaBlockPublisher> logger)
    {
        _logger = logger;
        _producer = new ProducerBuilder<string, string>(
            new ProducerConfig { BootstrapServers = configuration["Kafka:BootstrapServers"] }).Build();
    }

    public async Task PublishBlockAsync(RawBlock block, IReadOnlyList<RawTransaction> transactions, CancellationToken ct)
    {
        var key = block.ChainId.ToString();
        try
        {
            await _producer.ProduceAsync("raw-blocks",
                new Message<string, string> { Key = key, Value = JsonSerializer.Serialize(block) }, ct);

            foreach (var tx in transactions)
            {
                await _producer.ProduceAsync("raw-transactions",
                    new Message<string, string> { Key = key, Value = JsonSerializer.Serialize(tx) }, ct);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to publish block {BlockNumber} for chain {ChainId}", block.BlockNumber, block.ChainId);
        }
    }

    public void Dispose() => _producer?.Dispose();
}
