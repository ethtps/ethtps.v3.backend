
using Microsoft.Extensions.Configuration;

using Newtonsoft.Json;

namespace ETHTPS.V3.Cache.Core
{
    public sealed class AsyncCacheService
    {
        private readonly RedisCacheService _redisCache;
        private readonly RMQPublisher _rmqPublisher;
        private readonly RMQReceiver _rmqReceiver;

        public AsyncCacheService(string redisConfigurationString, string rmqHost)
        {
            _redisCache = new RedisCacheService(redisConfigurationString);
            _rmqPublisher = new RMQPublisher(rmqHost, Constants.QueueNames.Cache.REQUEST_QUEUE);
            _rmqReceiver = new RMQReceiver(rmqHost, Constants.QueueNames.Cache.RECEIVE_QUEUE);
        }

        public AsyncCacheService(IConfiguration configuration)
        {
            _redisCache = new RedisCacheService(configuration);
            var rmqSection = configuration.GetSection("RabbitMQ") ?? throw new ArgumentNullException("RabbitMQ section");
            var rmqHost = rmqSection.GetSection("Host").Value ?? throw new ArgumentNullException("RabbitMQ section");
            _rmqPublisher = new RMQPublisher(rmqHost, Constants.QueueNames.Cache.REQUEST_QUEUE);
            _rmqReceiver = new RMQReceiver(rmqHost, Constants.QueueNames.Cache.RECEIVE_QUEUE);
        }

        public async Task<string?> GetAsync(string key)
        {
            var value = await _redisCache.GetAsync(key);
            if (value == null)
            {
                CancellationTokenSource cancellationTokenSource = new(TimeSpan.FromSeconds(5));
                var token = cancellationTokenSource.Token;
                var received = false;
                //register for message
                _rmqReceiver.On(key, () =>
                {
                    received = true;
                });
                await _rmqPublisher.PublishAsync(key); // Request cache be built
                while (!received && !token.IsCancellationRequested)
                {
                    await Task.Delay(10);
                }
                if (received)
                {
                    return await _redisCache.GetAsync(key); //don't recurse, we should now have the value
                }
            }
            else
            {
                return value;
            }
            throw new OperationCanceledException($"Background service failed to build requested cache \"{key}\" in due time");
        }

        public async Task<T?> GetAsync<T>(string key)
        {
            string? value = await GetAsync(key);
            if (value == null) { return default(T?); }
            return JsonConvert.DeserializeObject<T>(value);
        }
    }
}
