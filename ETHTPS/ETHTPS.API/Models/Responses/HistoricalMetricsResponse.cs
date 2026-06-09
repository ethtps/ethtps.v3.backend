namespace ETHTPS.API.Models.Responses;

public record HistoricalMetricsResponse(
    int ChainId, string Resolution,
    DateTimeOffset From, DateTimeOffset To,
    IReadOnlyList<HistoricalBucket> Buckets);
