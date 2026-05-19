using ETHTPS.API.Models.Responses;
using ETHTPS.API.Repositories;

namespace ETHTPS.API.Services;

public class NetworkQueryService(NetworkReadRepository repository)
{
    public Task<IReadOnlyList<NetworkResponse>> GetAllActiveAsync(CancellationToken ct) =>
        repository.GetAllActiveAsync(ct);

    public Task<NetworkResponse?> GetByChainIdAsync(int chainId, CancellationToken ct) =>
        repository.GetByChainIdAsync(chainId, ct);

    public Task<(byte[] Bytes, string ContentType)?> GetLogoAsync(int chainId, CancellationToken ct) =>
        repository.GetLogoAsync(chainId, ct);
}
