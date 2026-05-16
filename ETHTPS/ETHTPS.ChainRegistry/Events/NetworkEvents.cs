namespace ETHTPS.ChainRegistry.Events;

public abstract record NetworkEvent(int ChainId, DateTimeOffset OccurredAt);

public record NetworkAddedEvent(int ChainId, string Name, string[] RpcUrls, bool IsTestnet, string NetworkType, DateTimeOffset OccurredAt)
    : NetworkEvent(ChainId, OccurredAt);

public record NetworkRemovedEvent(int ChainId, DateTimeOffset OccurredAt)
    : NetworkEvent(ChainId, OccurredAt);

public record NetworkRpcsUpdatedEvent(int ChainId, string[] NewRpcUrls, DateTimeOffset OccurredAt)
    : NetworkEvent(ChainId, OccurredAt);
