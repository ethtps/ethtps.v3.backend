using System.Net.Http.Json;

using ETHTPS.V3.LiveDataUpdater.RPC.Models.RequestModels;
using ETHTPS.V3.LiveDataUpdater.RPC.Models.ResponseModels;

namespace ETHTPS.V3.LiveDataUpdater.RPC
{
    public sealed class RPCClient : IRPCClient, IDisposable
    {
        private readonly string _baseURL;
        private readonly HttpClient _httpClient;

        public RPCClient(string baseURL)
        {
            _baseURL = baseURL;
            _httpClient = new HttpClient();
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }

        public async Task<BlockInfoResponseModel> GetBlockAsync(long blockNumber, CancellationToken cancellationToken)
        {
            var requestModel = new GetBlockByNumberRequestModel(blockNumber);
            var content = JsonContent.Create(requestModel);
            var response = await _httpClient.PostAsync(_baseURL, content, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<BlockInfoResponseModel>(cancellationToken) ?? throw new Exception("Endpoint returned null response");
        }

        public async Task<long> GetLatestBlockHeightAsync(CancellationToken cancellationToken)
        {
            var requestModel = new LatestBlockHeightRequestModel();
            var content = JsonContent.Create(requestModel);
            var response = await _httpClient.PostAsync(_baseURL, content, cancellationToken);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<LatestBlockHeightResponseModel>(cancellationToken) ?? throw new Exception("Endpoint returned null response");
            if (result.Result is null)
            {
                throw new Exception("Endpoint returned null value");
            }
            return Convert.ToInt64(result.Result, 16);
        }
    }
}
