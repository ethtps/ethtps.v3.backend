namespace ETHTPS.API.Models.Responses;

public record HistoricalBucket(
    DateTimeOffset Bucket,
    double? AvgTps, double? MaxTps,
    double? AvgGps, double? MaxGps,
    int BlockCount);
