namespace ETHTPS.Ingestion.Models;

public record Network
{
    public int ChainId { get; init; }
    public string Name { get; init; } = "";
    public string[] RpcUrls { get; init; } = [];
}
