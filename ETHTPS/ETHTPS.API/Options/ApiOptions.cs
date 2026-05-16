namespace ETHTPS.API.Options;

public class ApiOptions
{
    public int AnonymousRateLimitPerMinute { get; set; } = 60;
    public int AnonymousBurstSize { get; set; } = 30;
    public int ApiKeyRateLimitPerMinute { get; set; } = 600;
    public int ApiKeyBurstSize { get; set; } = 100;
    public int ApiKeyCacheTtlSeconds { get; set; } = 300;
}
