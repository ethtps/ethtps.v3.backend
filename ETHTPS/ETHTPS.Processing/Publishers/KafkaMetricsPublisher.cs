using System.Text.Json;
using Confluent.Kafka;
using ETHTPS.Processing.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ETHTPS.Processing.Publishers;

public sealed class KafkaMetricsPublisher : IMetricsPublisher, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaMetricsPublisher> _logger;

    public KafkaMetricsPublisher(IConfiguration configuration, ILogger<KafkaMetricsPublisher> logger)
    {
        _logger = logger;
        _producer = new ProducerBuilder<string, string>(
            new ProducerConfig { BootstrapServers = configuration["Kafka:BootstrapServers"] }).Build();
    }

    public async Task PublishAsync(MetricsComputedEvent evt, CancellationToken ct)
    {
        try
        {
            await _producer.ProduceAsync("aggregated-metrics",
                new Message<string, string>
                {
                    Key = evt.ChainId.ToString(),
                    Value = JsonSerializer.Serialize(evt)
                }, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to publish MetricsComputedEvent for chain {ChainId}", evt.ChainId);
        }
    }

    public void Dispose() => _producer?.Dispose();
}
