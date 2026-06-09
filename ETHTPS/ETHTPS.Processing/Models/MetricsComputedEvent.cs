namespace ETHTPS.Processing.Models;

public record MetricsComputedEvent
{
    public int ChainId { get; init; }
    public long BlockNumber { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public double? Tps { get; init; }
    public double? Gps { get; init; }
    public DateTimeOffset ComputedAt { get; init; }
}
