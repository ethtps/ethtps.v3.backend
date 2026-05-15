using System.Text.Json;
using System.Text.Json.Nodes;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ETHTPS.ChainRegistry.Kafka;

public sealed class KafkaProducer : IKafkaProducer, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaProducer> _logger;

    public KafkaProducer(IConfiguration configuration, ILogger<KafkaProducer> logger)
    {
        _logger = logger;
        _producer = new ProducerBuilder<string, string>(
            new ProducerConfig { BootstrapServers = configuration["Kafka:BootstrapServers"] }).Build();
    }

    public async Task PublishAsync<T>(string topic, string key, T value, CancellationToken ct)
    {
        try
        {
            var node = JsonSerializer.SerializeToNode(value)!.AsObject();
            node["type"] = typeof(T).Name;
            var json = node.ToJsonString();
            await _producer.ProduceAsync(topic, new Message<string, string> { Key = key, Value = json }, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to publish message to topic {Topic} with key {Key}", topic, key);
        }
    }

    public void Dispose() => _producer?.Dispose();
}
