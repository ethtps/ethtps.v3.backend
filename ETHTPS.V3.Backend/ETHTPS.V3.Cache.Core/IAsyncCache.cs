namespace ETHTPS.V3.Cache.Core
{
    public interface IAsyncCache
    {
        public Task<string?> GetAsync(string key);
        public Task<T?> GetAsync<T>(string key);
        public Task SetAsync(string key, string value);
        public Task SetAsync(string key, string value, TimeSpan expiry);
    }
}
