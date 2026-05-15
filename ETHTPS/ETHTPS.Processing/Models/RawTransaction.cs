namespace ETHTPS.Processing.Models;

public record RawTransaction
{
    public int ChainId { get; init; }
    public string BlockHash { get; init; } = "";
    public long BlockNumber { get; init; }
    public string TxHash { get; init; } = "";
    public ulong Gas { get; init; }
    public DateTimeOffset BlockTimestamp { get; init; }
}
