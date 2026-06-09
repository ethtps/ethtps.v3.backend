namespace ETHTPS.ChainRegistry.Kafka;

public interface IKafkaProducer
{
    Task PublishAsync<T>(string topic, string key, T value, CancellationToken ct);
}
