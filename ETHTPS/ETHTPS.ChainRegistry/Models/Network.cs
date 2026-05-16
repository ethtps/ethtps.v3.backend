namespace ETHTPS.ChainRegistry.Models;

public record Network
{
    public int ChainId { get; init; }
    public string Name { get; init; } = "";
    public string[] RpcUrls { get; init; } = [];
    public bool Enabled { get; init; } = true;
    public bool IsTestnet { get; init; } = false;
    public string NetworkType { get; init; } = "mainnet";
    public DateTimeOffset? RemovedAt { get; init; }
    public DateTimeOffset LastSyncedAt { get; init; }
}
