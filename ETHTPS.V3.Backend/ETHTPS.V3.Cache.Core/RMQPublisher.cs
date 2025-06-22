using System.Text;

using RabbitMQ.Client;

namespace ETHTPS.V3.Cache.Core
{
    public class RMQPublisher : RMQBase
    {
        public RMQPublisher(string hostname, string queueName) : base(hostname, queueName)
        {

        }

        public async Task PublishAsync(string message)
        {
            if (!_initialized)
            {
                _connection ??= await _connectionFactory.CreateConnectionAsync();
                _channel ??= await _connection.CreateChannelAsync();
                await _channel.QueueDeclareAsync(_queueName, true, false, false);
                _initialized = true;
            }
            await _channel!.BasicPublishAsync(string.Empty, _queueName, true, new BasicProperties(), body: Encoding.UTF8.GetBytes(message));
        }
    }
}
