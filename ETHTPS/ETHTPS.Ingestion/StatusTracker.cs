using System.Collections.Concurrent;

namespace ETHTPS.Ingestion;

public sealed record ChainStatus(
    int ChainId,
    string Name,
    long LastBlockNumber,
    int LastTxCount,
    long LastBlockTimeMs,
    DateTimeOffset LastBlockAt,
    string State); // "pending" | "ok" | "error" | "no-rpc"

public sealed class StatusTracker
{
    private readonly ConcurrentDictionary<int, ChainStatus> _statuses = new();

    public void Update(ChainStatus status) => _statuses[status.ChainId] = status;

    public IReadOnlyList<ChainStatus> GetAll() =>
        [.. _statuses.Values.OrderBy(s => s.ChainId)];
}
