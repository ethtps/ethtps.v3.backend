namespace ETHTPS.API.Models.Responses;

public record HistoricalBucket(
    DateTimeOffset Bucket,
    int ChainId,
    double? AvgTps, double? MaxTps,
    double? AvgGps, double? MaxGps,
    int BlockCount);
