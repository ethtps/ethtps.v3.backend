namespace ETHTPS.Backfill.Models;

public record ComputedMetrics
{
    public int ChainId { get; init; }
    public long BlockNumber { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public double? Tps { get; init; }
    public double? Gps { get; init; }
}
