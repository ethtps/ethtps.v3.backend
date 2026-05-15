using System.Diagnostics;

using ETHTPS.V3.Data.Models;
using ETHTPS.V3.LiveDataUpdater.RPC;
using ETHTPS.V3.LiveDataUpdater.RPC.Models.ResponseModels;

namespace ETHTPS.V3.LiveDataUpdater.LiveData
{
    public sealed class LiveDataRPCUpdater
    {
        private readonly IEnumerable<EndpointMetadata> _dataUpdaterMetadata;

        public LiveDataRPCUpdater(IEnumerable<EndpointMetadata> dataUpdaterMetadata)
        {
            _dataUpdaterMetadata = dataUpdaterMetadata;
        }

        public event EventHandler<EndpointMetadata>? OnFailure;
        public event EventHandler<(EndpointMetadata Metadata, double TimeMs)>? OnSuccess;
        public event EventHandler<(string Provider, BlockInfoResponseModel Block)>? OnNewBlockData;

        public async Task UpdateLatestBlockTransactionsAsync(CancellationToken token)
        {
            //TODO: order by health, average access time etc
            foreach (var metadata in _dataUpdaterMetadata.Where(x => x.Enabled).OrderBy(x => x.AverageAccessTimeMs))
            {
                using var rpcClient = new RPCClient(metadata.URL);
                try
                {
                    if (token.IsCancellationRequested) return;

                    CancellationTokenSource tokenSource = new(TimeSpan.FromSeconds(10));
                    var stopwatch = Stopwatch.StartNew();
                    var blockHeight = await rpcClient.GetLatestBlockHeightAsync(tokenSource.Token);
                    var latestBlock = await rpcClient.GetBlockAsync(blockHeight, tokenSource.Token);
                    if (latestBlock != null)
                    {
                        OnNewBlockData?.Invoke(this, (metadata.Provider, latestBlock));
                    }
                    stopwatch.Stop();
                    //ok if we get here
                    OnSuccess?.Invoke(this, (metadata, stopwatch.Elapsed.TotalMilliseconds));
                    return;
                }
                catch
                {
                    OnFailure?.Invoke(this, metadata);
                }
            }
        }
    }
}
