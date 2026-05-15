using Newtonsoft.Json;

namespace ETHTPS.V3.LiveDataUpdater.RPC.Models.ResponseModels
{
    public sealed class LatestBlockHeightResponseModel
    {
        [JsonProperty("jsonrpc")]
        public string? JSONRPC { get; set; }

        [JsonProperty("id")]
        public int ID { get; set; }

        [JsonProperty("result")]
        public string? Result { get; set; }
    }
}
