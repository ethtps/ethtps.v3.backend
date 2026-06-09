namespace ETHTPS.Ingestion.Models;

public record RawBlock
{
    public int ChainId { get; init; }
    public string BlockHash { get; init; } = "";
    public long BlockNumber { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public int TransactionCount { get; init; }
    public ulong GasUsed { get; init; }
    public ulong GasLimit { get; init; }
    public long BlockTimeMs { get; init; }
    public DateTimeOffset IngestedAt { get; init; }
}
