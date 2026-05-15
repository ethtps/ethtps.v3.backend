namespace ETHTPS.V3.LiveDataUpdater.RPC.Models.RequestModels
{
    public sealed class GetBlockByNumberRequestModel : RPCRequestModelBase
    {
        public GetBlockByNumberRequestModel(long blockNumber) : base("2.0", "eth_getBlockByNumber", new object[]
        {
           $"0X{blockNumber.ToString("X")}", false
        }, 1)
        {

        }
    }
}
