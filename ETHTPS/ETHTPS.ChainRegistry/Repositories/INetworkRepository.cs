using ETHTPS.ChainRegistry.Models;

namespace ETHTPS.ChainRegistry.Repositories;

public interface INetworkRepository
{
    Task EnsureSchemaAsync(CancellationToken ct);
    Task<IReadOnlyList<Network>> GetAllAsync(CancellationToken ct);
    Task UpsertAsync(Network network, CancellationToken ct);
    Task MarkRemovedAsync(int chainId, CancellationToken ct);
}
