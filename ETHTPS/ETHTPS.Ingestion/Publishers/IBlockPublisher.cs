using ETHTPS.Ingestion.Models;

namespace ETHTPS.Ingestion.Publishers;

public interface IBlockPublisher
{
    Task PublishBlockAsync(RawBlock block, IReadOnlyList<RawTransaction> transactions, CancellationToken ct);
}
