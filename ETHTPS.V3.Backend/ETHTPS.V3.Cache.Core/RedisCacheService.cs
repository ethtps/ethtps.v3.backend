using Microsoft.Extensions.Configuration;

using Newtonsoft.Json;

using StackExchange.Redis;

namespace ETHTPS.V3.Cache.Core
{
    public sealed class RedisCacheService : ISyncCache, IAsyncCache
    {
        private readonly ConnectionMultiplexer _multiplexer;
        private readonly IDatabase _database;

        public RedisCacheService(string configurationString)
        {
            _multiplexer = ConnectionMultiplexer.Connect(configurationString);
            _database = _multiplexer.GetDatabase();
        }

        public RedisCacheService(IConfiguration configuration)
        {
            var section = configuration.GetSection("Redis") ?? throw new ArgumentNullException("Redis configuration section");
            var value = section.GetSection("ConnectionString").Value ?? throw new ArgumentNullException("Redis configuration string");
            _multiplexer = ConnectionMultiplexer.Connect(value);
            _database = _multiplexer.GetDatabase();
        }

        public string? Get(string key) => _database.StringGet(key);

        public async Task<string?> GetAsync(string key) => await _database.StringGetAsync(key);

        public T? Get<T>(string key)
        {
            var value = Get(key);
            if (string.IsNullOrEmpty(value))
            {
                return JsonConvert.DeserializeObject<T>(value!);
            }
            return default;
        }

        public async Task<T?> GetAsync<T>(string key)
        {
            var value = await GetAsync(key);
            if (string.IsNullOrEmpty(value))
            {
                return JsonConvert.DeserializeObject<T>(value!);
            }
            return default;
        }

        public void Set(string key, string value) => _database.StringSet(key, value);

        public void Set<T>(string key, T value) => Set(key, JsonConvert.SerializeObject(value));

        public async Task SetAsync(string key, string value) => await _database.StringSetAsync(key, value);

        public void Set(string key, string value, TimeSpan expiry) => _database.StringSet(key, value, expiry);

        public void Set<T>(string key, T value, TimeSpan expiry) => Set(key, JsonConvert.SerializeObject(value), expiry);

        public async Task SetAsync(string key, string value, TimeSpan expiry) => await _database.StringSetAsync(key, value, expiry);
    }
}
