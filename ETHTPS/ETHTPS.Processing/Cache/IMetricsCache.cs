using ETHTPS.Processing.Models;

namespace ETHTPS.Processing.Cache;

public interface IMetricsCache
{
    Task SetAsync(ComputedMetrics metrics, CancellationToken ct);
}
