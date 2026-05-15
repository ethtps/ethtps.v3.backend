namespace ETHTPS.Ingestion.Models;

public record NetworkAddedEvent
{
    public string Type { get; init; } = "";
    public int ChainId { get; init; }
    public string Name { get; init; } = "";
    public string[] RpcUrls { get; init; } = [];
    public DateTimeOffset OccurredAt { get; init; }
}

public record NetworkRemovedEvent
{
    public string Type { get; init; } = "";
    public int ChainId { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
}

public record NetworkRpcsUpdatedEvent
{
    public string Type { get; init; } = "";
    public int ChainId { get; init; }
    public string[] NewRpcUrls { get; init; } = [];
    public DateTimeOffset OccurredAt { get; init; }
}
