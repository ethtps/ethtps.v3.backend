using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using MongoDB.Driver;

namespace ETHTPS.V3.AbstractionLayer.Extensions
{
    public static class DependencyInjectionExtensions
    {
        public static void AddMongoDB(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetValue<string>("MongoDbSettings:ConnectionString");
            var settings = MongoClientSettings.FromUrl(new MongoUrl(connectionString));
            services.AddSingleton<IMongoClient>(new MongoClient(settings));
        }
    }
}
