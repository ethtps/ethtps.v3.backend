namespace ETHTPS.Backfill.Models;

public record BackfillProgress
{
    public int ChainId { get; init; }
    public long LastBlockNumber { get; init; } = -1;
    public long? TargetBlock { get; init; }
    public string Status { get; init; } = "pending";
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; init; }
}
