using ETHTPS.Processing.Models;

namespace ETHTPS.Processing.Publishers;

public interface IMetricsPublisher
{
    Task PublishAsync(MetricsComputedEvent evt, CancellationToken ct);
}
