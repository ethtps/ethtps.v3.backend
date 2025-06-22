using System.Collections.Concurrent;
using System.Text;

using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace ETHTPS.V3.Cache.Core
{
    public class RMQReceiver : RMQBase
    {
        private readonly ConcurrentDictionary<string, Queue<Action>> _queuedActions = new();
        private readonly ConcurrentDictionary<string, List<Action>> _queuedPersistentActions = new();
        private AsyncEventingBasicConsumer? _consumer;

        public RMQReceiver(string hostname, string queueName) : base(hostname, queueName)
        {
            Task.Run(InitializeAsync).Wait();
        }

        private async Task InitializeAsync()
        {
            if (!_initialized)
            {
                _connection ??= await _connectionFactory.CreateConnectionAsync();
                _channel ??= await _connection.CreateChannelAsync();
                await _channel.QueueDeclareAsync(_queueName, true, false, false);
                _initialized = true;
            }
            _consumer = new AsyncEventingBasicConsumer(_channel!);
            _consumer.ReceivedAsync += async (ch, ea) =>
            {
                var body = ea.Body.ToArray();
                var decoded = Encoding.UTF8.GetString(body);
                Console.WriteLine($"Received message from queue \"{_queueName}\": \"{decoded}\"");
                if (_queuedPersistentActions.TryGetValue(decoded, out List<Action>? queuedPersistentActions))
                {
                    foreach (var action in queuedPersistentActions)
                    {
                        action();
                    }
                }
                if (_queuedActions.TryGetValue(decoded, out Queue<Action>? queue))
                {
                    while (queue.Count > 0)
                    {
                        var action = queue.Dequeue();
                        action();
                    }
                }
                await _channel!.BasicAckAsync(ea.DeliveryTag, false);
            };
            await _channel!.BasicConsumeAsync(_queueName, false, _consumer);
        }

        public void On(string message, Action action, bool persist = false)
        {
            if (!persist)
            {
                if (!_queuedActions.ContainsKey(message))
                {
                    if (_queuedActions.TryAdd(message, new()))
                    {
                        _queuedActions[message].Enqueue(action);
                    }
                    else
                    {
                        throw new Exception("Failed to register action");
                    }
                }
            }
            else
            {
                if (!_queuedPersistentActions.ContainsKey(message))
                {
                    if (_queuedPersistentActions.TryAdd(message, new()))
                    {
                        _queuedPersistentActions[message].Add(action);
                    }
                    else
                    {
                        throw new Exception("Failed to register action");
                    }
                }
            }
        }
    }
}
