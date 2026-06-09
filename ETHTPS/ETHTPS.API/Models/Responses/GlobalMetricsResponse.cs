namespace ETHTPS.API.Models.Responses;

public record GlobalMetricsResponse(
    double TotalTps, double TotalGps,
    int ActiveChains, DateTimeOffset ComputedAt);
