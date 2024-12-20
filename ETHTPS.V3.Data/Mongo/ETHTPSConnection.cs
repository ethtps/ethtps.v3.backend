using ETHTPS.V3.Data.Mongo.Models;

using MongoDB.Bson;
using MongoDB.Driver;

namespace ETHTPS.V3.Data.Mongo
{
    public sealed class ETHTPSConnection : IMongoConnection
    {
        private readonly IMongoClient _mongoClient;
        private readonly IMongoDatabase _database;
        private const string _DB_NAME = "ethtps";
        private const string _CHAIN_LIST_COLLECTION_NAME = "networks";
        public ETHTPSConnection(IMongoClient mongoClient)
        {
            _mongoClient = mongoClient ?? throw new ArgumentNullException(nameof(mongoClient));
            _database = _mongoClient.GetDatabase(_DB_NAME);
        }

        public async Task<IEnumerable<string>> GetAllCollectionsAsync() => await _database.ListCollectionNames().ToListAsync();

        public async Task<List<ChainInfo>> GetAllChainsAsync()
        {
            var collection = _database.GetCollection<ChainInfo>(_CHAIN_LIST_COLLECTION_NAME);
            return await collection.Find(new BsonDocument()).ToListAsync();
        }

        public void Dispose()
        {

        }
    }
}
