namespace ETHTPS.V3.LiveDataUpdater.RPC.Models.RequestModels
{
    public sealed class LatestBlockHeightRequestModel : RPCRequestModelBase
    {
        public LatestBlockHeightRequestModel() : base("2.0", "eth_blockNumber", null, 1)
        {

        }
    }
}
