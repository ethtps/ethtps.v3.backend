namespace ETHTPS.ChainRegistry.Options;

public class ChainRegistryOptions
{
    public TimeSpan SyncInterval { get; set; } = TimeSpan.FromHours(1);
    public string ChainlistUrl { get; set; } = "https://chainid.network/chains.json";
}
