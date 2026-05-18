namespace ETHTPS.API.Models.Responses;

public sealed record ChainIngestionStatus(
    int ChainId,
    string Name,
    long LastBlockNumber,
    int LastTxCount,
    long LastBlockTimeMs,
    DateTimeOffset LastBlockAt,
    string State,
    bool IsStale);

public sealed record IngestionStatusResponse(
    DateTimeOffset ReportedAt,
    int ChainCount,
    IReadOnlyList<ChainIngestionStatus> Chains);
