using RabbitMQ.Client;

namespace ETHTPS.V3.Cache.Core
{
    public class RMQBase
    {
        protected IConnection? _connection;
        protected IChannel? _channel;
        protected bool _initialized = false;
        protected readonly ConnectionFactory _connectionFactory;
        protected readonly string _queueName;

        protected RMQBase(string hostname, string queueName)
        {
            _connectionFactory = new ConnectionFactory() { HostName = hostname };
            _queueName = queueName;
        }
    }
}
