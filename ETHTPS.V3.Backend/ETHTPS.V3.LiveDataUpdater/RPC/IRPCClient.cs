using ETHTPS.V3.LiveDataUpdater.RPC.Models.ResponseModels;

namespace ETHTPS.V3.LiveDataUpdater.RPC
{
    public interface IRPCClient
    {
        public Task<long> GetLatestBlockHeightAsync(CancellationToken cancellationToken);

        public Task<BlockInfoResponseModel> GetBlockAsync(long blockNumber, CancellationToken cancellationToken);
    }
}
