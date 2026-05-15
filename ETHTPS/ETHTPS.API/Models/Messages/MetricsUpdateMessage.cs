namespace ETHTPS.API.Models.Messages;

public record MetricsUpdateMessage(
    int ChainId, double? Tps, double? Gps,
    long BlockNumber, DateTimeOffset Timestamp);
