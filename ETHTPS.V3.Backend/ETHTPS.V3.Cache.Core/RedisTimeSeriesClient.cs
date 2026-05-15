using Microsoft.Extensions.Configuration;

using StackExchange.Redis;

namespace ETHTPS.V3.Cache.Core
{
    public sealed class RedisTimeSeriesClient
    {
        private readonly ConnectionMultiplexer _multiplexer;
        private readonly IDatabase _database;

        public RedisTimeSeriesClient(string configurationString)
        {
            _multiplexer = ConnectionMultiplexer.Connect(configurationString);
            _database = _multiplexer.GetDatabase();
        }

        public RedisTimeSeriesClient(IConfiguration configuration)
        {
            var section = configuration.GetSection("Redis") ?? throw new ArgumentNullException("Redis configuration section");
            var value = section.GetSection("ConnectionString").Value ?? throw new ArgumentNullException("Redis configuration string");
            _multiplexer = ConnectionMultiplexer.Connect(value);
            _database = _multiplexer.GetDatabase();
        }
    }
}
