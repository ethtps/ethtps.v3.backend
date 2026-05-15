namespace ETHTPS.API.Models.Responses;

public record LiveMetricsResponse(
    int ChainId, double? Tps, double? Gps, long BlockNumber, DateTimeOffset Timestamp);
