namespace ETHTPS.V3.Cache.Core
{
    public interface ISyncCache
    {
        public string? Get(string key);
        public void Set(string key, string value);
        public void Set<T>(string key, T value);
        public void Set(string key, string value, TimeSpan expiry);
        public void Set<T>(string key, T value, TimeSpan expiry);
    }
}
