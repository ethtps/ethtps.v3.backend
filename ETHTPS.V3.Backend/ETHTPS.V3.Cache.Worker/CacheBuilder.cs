using ETHTPS.V3.Cache.Core;

namespace ETHTPS.V3.Cache.Worker
{
    internal class CacheBuilder
    {
        private readonly RedisCacheService _redisCache;
        private readonly RMQPublisher _rmqPublisher;
        private readonly RMQReceiver _rmqReceiver;

        public CacheBuilder(string redisConfigurationString, string rmqHost)
        {
            _redisCache = new RedisCacheService(redisConfigurationString);
            _rmqPublisher = new RMQPublisher(rmqHost, Constants.QueueNames.Cache.RECEIVE_QUEUE); // inverted listeners and publishers
            _rmqReceiver = new RMQReceiver(rmqHost, Constants.QueueNames.Cache.REQUEST_QUEUE);
        }

        public async Task RunAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(1000);
            }
        }

        public void RegisterAction<T>(string cacheKey, Func<T> builderAction)
        {
            _rmqReceiver.On(cacheKey, async () =>
            {
                _redisCache.Set(cacheKey, builderAction());
                await _rmqPublisher.PublishAsync(cacheKey);
            }, persist: true);
            Console.WriteLine($"Registered action \"{cacheKey}\"");
        }
    }
}
