namespace ETHTPS.V3.Data.Mongo
{
    public interface IMongoConnection : IDisposable
    {
        public Task<IEnumerable<string>> GetAllCollectionsAsync();
    }
}
