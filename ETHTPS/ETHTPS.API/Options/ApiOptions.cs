namespace ETHTPS.API.Options;

public class ApiOptions
{
    public int AnonymousRateLimitPerMinute { get; set; } = 60;
    public int ApiKeyRateLimitPerMinute { get; set; } = 600;
    public int ApiKeyCacheTtlSeconds { get; set; } = 300;
}
